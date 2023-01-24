using oni;
using Rhythm.Net;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Bonsai.OpenEphys
{
    [Description("Produces a sequence of buffered samples acquired from the Open Ephys acquisition board equipped with the Open Ephys FPGA.")]
    public class AcquisitionBoard : Bonsai.Source<OpenEphysRhythmDataFrame>
    {
        const string BoardCategory = "Board Settings";
        const string CableDelayCategory = "Delay Settings";
        const uint initLen = 64;
        ONIRhythmBoard board;

        const int ChipIdRhd2132 = 1;
        const int ChipIdRhd2216 = 2;
        const int ChipIdRhd2164 = 4;
        double cableLengthPortA;
        double cableLengthPortB;
        double cableLengthPortC;
        double cableLengthPortD;
        uint lastMemUSage;
        uint memoryWords;

        public AcquisitionBoard()
        {
            SampleRate = AmplifierSampleRate.SampleRate20000Hz;
            LowerBandwidth = 0.1;
            UpperBandwidth = 7500.0;
            DspCutoffFrequency = 1.0;
            DspEnabled = true;
            BoardIndex = -1;
            BufferCount = 256;
            board = null;
            lastMemUSage = 0;
            using (var ctx = new ONIRhythmBoard(-1))
            {
                SetGatewareVersion(ctx.GetGatewareVersion());
            }
        }

        [Category(BoardCategory)]
        [Range(-1,100)]
        [Description("The index of the board to open (-1 to open the first available board).")]
        public int BoardIndex { get; set; }

        [Category(BoardCategory)]
        [Range(1, 1024)]
        [Description("Number of samples to group into a single frame")]
        public int BufferCount { get; set; }

        [Category(BoardCategory)]
        [Description("The per-channel sampling rate.")]
        public AmplifierSampleRate SampleRate { get; set; }

        [Category(BoardCategory)]
        [Description("Specifies whether the external fast settle channel (channel 0) is enabled.")]
        public bool ExternalFastSettleEnabled { get; set; }

        [Category(BoardCategory)]
        [Description("The lower bandwidth of the amplifier on-board DSP filter (Hz).")]
        public double LowerBandwidth { get; set; }

        [Category(BoardCategory)]
        [Description("The upper bandwidth of the amplifier on-board DSP filter (Hz).")]
        public double UpperBandwidth { get; set; }

        [Category(BoardCategory)]
        [Description("The cutoff frequency of the DSP offset removal filter (Hz).")]
        public double DspCutoffFrequency { get; set; }

        [Category(BoardCategory)]
        [Externalizable(false)]
        [ReadOnly(true)]
        public string GatewareVersion { get; set; }

        [Category(BoardCategory)]
        [Description("Specifies whether the DSP offset removal filter is enabled.")]
        public bool DspEnabled { get; set; }

        [Category(CableDelayCategory)]
        [Description("The optional delay for sampling the MISO line in port A, in integer clock steps.")]
        public int? CableDelayA { get; set; }

        [Category(CableDelayCategory)]
        [Description("The optional delay for sampling the MISO line in port B, in integer clock steps.")]
        public int? CableDelayB { get; set; }

        [Category(CableDelayCategory)]
        [Description("The optional delay for sampling the MISO line in port C, in integer clock steps.")]
        public int? CableDelayC { get; set; }

        [Category(CableDelayCategory)]
        [Description("The optional delay for sampling the MISO line in port D, in integer clock steps.")]
        public int? CableDelayD { get; set; }

        private void SetGatewareVersion(uint ver)
        {
            GatewareVersion = "v" + ((ver >> 8) & 0xFF).ToString() + "." + (ver & 0xFF).ToString();
        }

        public override IObservable<OpenEphysRhythmDataFrame> Generate()
        {
            return Observable.Create<OpenEphysRhythmDataFrame>((observer, cancellationToken) =>
            {
                return Task.Factory.StartNew(() =>
               {
                   try
                   {
                       board = new ONIRhythmBoard(BoardIndex);
                       InitSequence();
                       SetGatewareVersion(board.GetGatewareVersion());
                       board.SetTtlMode(0);
                       board.SetTtlOut(0);
                       board.SetContinuousRunMode(true);
                       board.EnableExternalFastSettle(ExternalFastSettleEnabled);
                       board.setMemDevice(BufferCount, out memoryWords);

                       board.Run();
                       while (!cancellationToken.IsCancellationRequested)
                       {
                           var dataBlock = board.readData(BufferCount, cancellationToken, ref lastMemUSage);
                           var frame = new OpenEphysRhythmDataFrame(dataBlock, (double)lastMemUSage / memoryWords * 100.0);
                           observer.OnNext(frame);
                       }
                   }
                   finally
                   {
                       board.SetContinuousRunMode(false);
                       board.SetMaxTimeStep(0);
                       board.Dispose();
                       board = null;
                   }

               },
                cancellationToken,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
            })
            .PublishReconnectable()
            .RefCount();
        }

        private void InitSequence()
        {
            // Initialize interface board.
            board.Initialize();

            // Set sample rate and upload all auxiliary SPI command sequences.
            ChangeSampleRate(SampleRate);

            UploadCommonCommands();

            // Run ADC calibration
            RunCalibration();

            // Set default configuration for all eight DACs on interface board.
            SetDacDefaultConfiguration();

            // Find amplifier chips connected to interface board and compute their
            // optimal delay parameters.
            ScanConnectedAmplifiers();
        }


        void ChangeSampleRate(AmplifierSampleRate amplifierSampleRate)
        {
            board.SetSampleRate(amplifierSampleRate);

            // Now that we have set our sampling rate, we can set the MISO sampling delay
            // which is dependent on the sample rate.
            board.SetCableLengthMeters(BoardPort.PortA, cableLengthPortA);
            board.SetCableLengthMeters(BoardPort.PortB, cableLengthPortB);
            board.SetCableLengthMeters(BoardPort.PortC, cableLengthPortC);
            board.SetCableLengthMeters(BoardPort.PortD, cableLengthPortD);

            // Set up an RHD2000 register object using this sample rate to
            // optimize MUX-related register settings.
            var sampleRate = board.GetSampleRate();
            Rhd2000Registers chipRegisters = new Rhd2000Registers(sampleRate);
            var commandList = new List<int>();



            // For the AuxCmd3 slot, we will create three command sequences.  All sequences
            // will configure and read back the RHD2000 chip registers, but one sequence will
            // also run ADC calibration.  Another sequence will enable amplifier 'fast settle'.

            // Before generating register configuration command sequences, set amplifier
            // bandwidth parameters.
            chipRegisters.SetDspCutoffFreq(DspCutoffFrequency);
            chipRegisters.SetLowerBandwidth(LowerBandwidth);
            chipRegisters.SetUpperBandwidth(UpperBandwidth);
            chipRegisters.EnableDsp(DspEnabled);

            // Upload version with ADC calibration to AuxCmd3 RAM Bank 0.
            var sequenceLength = chipRegisters.CreateCommandListRegisterConfig(commandList, true);
            board.UploadCommandList(commandList, AuxCmdSlot.AuxCmd3, 0);
            board.SelectAuxCommandLength(AuxCmdSlot.AuxCmd3, 0, sequenceLength - 1);

            // Upload version with no ADC calibration to AuxCmd3 RAM Bank 1.
            sequenceLength = chipRegisters.CreateCommandListRegisterConfig(commandList, false);
            board.UploadCommandList(commandList, AuxCmdSlot.AuxCmd3, 1);
            board.SelectAuxCommandLength(AuxCmdSlot.AuxCmd3, 0, sequenceLength - 1);

            // Upload version with fast settle enabled to AuxCmd3 RAM Bank 2.
            chipRegisters.SetFastSettle(true);
            sequenceLength = chipRegisters.CreateCommandListRegisterConfig(commandList, false);
            board.UploadCommandList(commandList, AuxCmdSlot.AuxCmd3, 2);
            board.SelectAuxCommandLength(AuxCmdSlot.AuxCmd3, 0, sequenceLength - 1);
            chipRegisters.SetFastSettle(false);

            UpdateRegisterConfiguration(fastSettle: false);
        }

        /// <summary>
        /// Uploads command lists not related to sample rate, so they only need to be set once.
        /// </summary>
        void UploadCommonCommands()
        {
            // Set up an RHD2000 register object using this sample rate to
            // optimize MUX-related register settings.
            var sampleRate = board.GetSampleRate();
            Rhd2000Registers chipRegisters = new Rhd2000Registers(sampleRate);
            var commandList = new List<int>();

            // Create a command list for the AuxCmd1 slot.  This command sequence will create a 250 Hz,
            // zero-amplitude sine wave (i.e., a flatline).  We will change this when we want to perform
            // impedance testing.
            var sequenceLength = chipRegisters.CreateCommandListZcheckDac(commandList, 250, 0);
            board.UploadCommandList(commandList, AuxCmdSlot.AuxCmd1, 0);
            board.SelectAuxCommandLength(AuxCmdSlot.AuxCmd1, 0, sequenceLength - 1);
            board.SelectAuxCommandBank(BoardPort.PortA, AuxCmdSlot.AuxCmd1, 0);
            board.SelectAuxCommandBank(BoardPort.PortB, AuxCmdSlot.AuxCmd1, 0);
            board.SelectAuxCommandBank(BoardPort.PortC, AuxCmdSlot.AuxCmd1, 0);
            board.SelectAuxCommandBank(BoardPort.PortD, AuxCmdSlot.AuxCmd1, 0);

            // Next, we'll create a command list for the AuxCmd2 slot.  This command sequence
            // will sample the temperature sensor and other auxiliary ADC inputs.
            sequenceLength = chipRegisters.CreateCommandListTempSensor(commandList);
            board.UploadCommandList(commandList, AuxCmdSlot.AuxCmd2, 0);
            board.SelectAuxCommandLength(AuxCmdSlot.AuxCmd2, 0, sequenceLength - 1);
            board.SelectAuxCommandBank(BoardPort.PortA, AuxCmdSlot.AuxCmd2, 0);
            board.SelectAuxCommandBank(BoardPort.PortB, AuxCmdSlot.AuxCmd2, 0);
            board.SelectAuxCommandBank(BoardPort.PortC, AuxCmdSlot.AuxCmd2, 0);
            board.SelectAuxCommandBank(BoardPort.PortD, AuxCmdSlot.AuxCmd2, 0);


        }

        void UpdateRegisterConfiguration(bool fastSettle)
        {
            board.SelectAuxCommandBank(BoardPort.PortA, AuxCmdSlot.AuxCmd3, fastSettle ? 2 : 1);
            board.SelectAuxCommandBank(BoardPort.PortB, AuxCmdSlot.AuxCmd3, fastSettle ? 2 : 1);
            board.SelectAuxCommandBank(BoardPort.PortC, AuxCmdSlot.AuxCmd3, fastSettle ? 2 : 1);
            board.SelectAuxCommandBank(BoardPort.PortD, AuxCmdSlot.AuxCmd3, fastSettle ? 2 : 1);
        }

        void RunCalibration()
        {
            // Select RAM Bank 0 for AuxCmd3 initially, so the ADC is calibrated.
            board.SelectAuxCommandBank(BoardPort.PortA, AuxCmdSlot.AuxCmd3, 0);
            board.SelectAuxCommandBank(BoardPort.PortB, AuxCmdSlot.AuxCmd3, 0);
            board.SelectAuxCommandBank(BoardPort.PortC, AuxCmdSlot.AuxCmd3, 0);
            board.SelectAuxCommandBank(BoardPort.PortD, AuxCmdSlot.AuxCmd3, 0);

            // Since our longest command sequence is 60 commands, we run the SPI
            // interface for 60 samples 
            board.SetMaxTimeStep(initLen);
            board.SetContinuousRunMode(false);

            // Run ADC calibration command sequence
            board.Run();
            while (board.IsRunning()) Thread.Sleep(0);
            board.Stop();

            // Now that ADC calibration has been performed, we switch to the command sequence
            // that does not execute ADC calibration.
            UpdateRegisterConfiguration(fastSettle: false);
        }

        void SetDacDefaultConfiguration()
        {
            for (int i = 0; i < 8; i++)
            {
                board.EnableDac(i, false);
                board.SelectDacDataStream(i, 0);
                board.SelectDacDataChannel(i, 0);
            }

            board.SetDacManual(32768);
            board.SetDacGain(0);
            board.SetAudioNoiseSuppress(0);
        }

        void ScanConnectedAmplifiers()
        {
            // Set sampling rate to highest value for maximum temporal resolution.
            ChangeSampleRate(AmplifierSampleRate.SampleRate30000Hz);

            // Enable all data streams, and set sources to cover one or two chips
            // on Ports A-D.
            board.SetDataSource(0, BoardDataSource.PortA1);
            board.SetDataSource(1, BoardDataSource.PortA2);
            board.SetDataSource(2, BoardDataSource.PortB1);
            board.SetDataSource(3, BoardDataSource.PortB2);
            board.SetDataSource(4, BoardDataSource.PortC1);
            board.SetDataSource(5, BoardDataSource.PortC2);
            board.SetDataSource(6, BoardDataSource.PortD1);
            board.SetDataSource(7, BoardDataSource.PortD2);

            var maxNumDataStreams = ONIRhythmBoard.MAX_DATA_STREAMS;
            for (int i = 0; i < maxNumDataStreams; i++)
            {
                board.EnableDataStream(i, true);
            }
            board.UpdateStreamBlockSize();

            // Select RAM Bank 0 for AuxCmd3
            board.SelectAuxCommandBank(BoardPort.PortA, AuxCmdSlot.AuxCmd3, 0);
            board.SelectAuxCommandBank(BoardPort.PortB, AuxCmdSlot.AuxCmd3, 0);
            board.SelectAuxCommandBank(BoardPort.PortC, AuxCmdSlot.AuxCmd3, 0);
            board.SelectAuxCommandBank(BoardPort.PortD, AuxCmdSlot.AuxCmd3, 0);

            //We run this for long enough to get enough buffers in the
            board.SetMaxTimeStep(128*initLen);
            board.SetContinuousRunMode(false);

            // Run SPI command sequence at all 16 possible FPGA MISO delay settings
            // to find optimum delay for each SPI interface cable.
            var maxNumChips = 8;
            var chipId = new int[maxNumChips];
            var optimumDelays = new int[maxNumChips];
            var secondDelays = new int[maxNumChips];
            var goodDelayCounts = new int[maxNumChips];
            for (int i = 0; i < optimumDelays.Length; i++)
            {
                optimumDelays[i] = -1;
                secondDelays[i] = -1;
                goodDelayCounts[i] = 0;
            }

            for (int delay = 0; delay < 16; delay++)
            {
                board.SetCableDelay(BoardPort.PortA, delay);
                board.SetCableDelay(BoardPort.PortB, delay);
                board.SetCableDelay(BoardPort.PortC, delay);
                board.SetCableDelay(BoardPort.PortD, delay);

                // Run SPI command sequence
                board.Run();
                var dataBlock = board.readData(64,CancellationToken.None,ref lastMemUSage);
                board.Stop();

                // Read the Intan chip ID number from each RHD2000 chip found.
                // Record delay settings that yield good communication with the chip.
                for (int chipIdx = 0; chipIdx < chipId.Length; chipIdx++)
                {
                    int register59Value;
                    var id = ReadDeviceId(dataBlock, chipIdx, out register59Value);
                    if (id > 0)
                    {
                        chipId[chipIdx] = id;
                        goodDelayCounts[chipIdx]++;
                        switch (goodDelayCounts[chipIdx])
                        {
                            case 1: optimumDelays[chipIdx] = delay; break;
                            case 2: secondDelays[chipIdx] = delay; break;
                            case 3: optimumDelays[chipIdx] = secondDelays[chipIdx]; break;
                        }
                    }
                }
            }

            // Now that we know which RHD2000 amplifier chips are plugged into each SPI port,
            // add up the total number of amplifier channels on each port and calculate the number
            // of data streams necessary to convey this data over the USB interface.
            var numStreamsRequired = 0;
            var rhd2216ChipPresent = false;
            for (int chipIdx = 0; chipIdx < chipId.Length; ++chipIdx)
            {
                switch (chipId[chipIdx])
                {
                    case ChipIdRhd2216:
                        numStreamsRequired++;
                        rhd2216ChipPresent = true;
                        break;
                    case ChipIdRhd2132:
                        numStreamsRequired++;
                        break;
                    case ChipIdRhd2164:
                        numStreamsRequired += 2;
                        break;
                    default:
                        break;
                }
            }

            // If the user plugs in more chips than the USB interface can support, throw an exception
            if (numStreamsRequired > maxNumDataStreams)
            {
                var capacityExceededMessage = "Capacity of USB Interface Exceeded. This RHD2000 USB interface board can only support {0} amplifier channels.";
                if (rhd2216ChipPresent) capacityExceededMessage += " (Each RHD2216 chip counts as 32 channels for USB interface purposes.)";
                capacityExceededMessage = string.Format(capacityExceededMessage, maxNumDataStreams * 32);
                throw new InvalidOperationException(capacityExceededMessage);
            }

            // Reconfigure USB data streams in consecutive order to accommodate all connected chips.
            int activeStream = 0;
            for (int chipIdx = 0; chipIdx < chipId.Length; ++chipIdx)
            {
                if (chipId[chipIdx] > 0)
                {
                    board.EnableDataStream(activeStream, true);
                    board.SetDataSource(activeStream, (BoardDataSource)chipIdx);
                    if (chipId[chipIdx] == ChipIdRhd2164)
                    {
                        board.EnableDataStream(activeStream + 1, true);
                        board.SetDataSource(activeStream + 1, (BoardDataSource)(chipIdx + BoardDataSource.PortA1Ddr));
                        activeStream += 2;
                    }
                    else activeStream++;
                }
                else optimumDelays[chipIdx] = 0;
            }

            // Now, disable data streams where we did not find chips present.
            for (; activeStream < maxNumDataStreams; activeStream++)
            {
                board.EnableDataStream(activeStream, false);
            }
            board.UpdateStreamBlockSize();
            // Set cable delay settings that yield good communication with each
            // RHD2000 chip.
            var optimumDelayA = CableDelayA.GetValueOrDefault(Math.Max(optimumDelays[0], optimumDelays[1]));
            var optimumDelayB = CableDelayB.GetValueOrDefault(Math.Max(optimumDelays[2], optimumDelays[3]));
            var optimumDelayC = CableDelayC.GetValueOrDefault(Math.Max(optimumDelays[4], optimumDelays[5]));
            var optimumDelayD = CableDelayD.GetValueOrDefault(Math.Max(optimumDelays[6], optimumDelays[7]));
            board.SetCableDelay(BoardPort.PortA, optimumDelayA);
            board.SetCableDelay(BoardPort.PortB, optimumDelayB);
            board.SetCableDelay(BoardPort.PortC, optimumDelayC);
            board.SetCableDelay(BoardPort.PortD, optimumDelayD);
            cableLengthPortA = board.EstimateCableLengthMeters(optimumDelayA);
            cableLengthPortB = board.EstimateCableLengthMeters(optimumDelayB);
            cableLengthPortC = board.EstimateCableLengthMeters(optimumDelayC);
            cableLengthPortD = board.EstimateCableLengthMeters(optimumDelayD);

            // Return sample rate to original user-selected value.
            ChangeSampleRate(SampleRate);
        }

        int ReadDeviceId(RhythmData dataBlock, int stream, out int register59Value)
        {
            // First, check ROM registers 32-36 to verify that they hold 'INTAN', and
            // the initial chip name ROM registers 24-26 that hold 'RHD'.
            // This is just used to verify that we are getting good data over the SPI
            // communication channel.
            
            //Due to the way the firmware works, all aux results are shifted one sample from the original codebase
            var intanChipPresent = (
                (char)dataBlock.AuxData[stream][2, 32 + 1] == 'I' &&
                (char)dataBlock.AuxData[stream][2, 33 + 1] == 'N' &&
                (char)dataBlock.AuxData[stream][2, 34 + 1] == 'T' &&
                (char)dataBlock.AuxData[stream][2, 35 + 1] == 'A' &&
                (char)dataBlock.AuxData[stream][2, 36 + 1] == 'N' &&
                (char)dataBlock.AuxData[stream][2, 24 + 1] == 'R' &&
                (char)dataBlock.AuxData[stream][2, 25 + 1] == 'H' &&
                (char)dataBlock.AuxData[stream][2, 26 + 1] == 'D');

            // If the SPI communication is bad, return -1.  Otherwise, return the Intan
            // chip ID number stored in ROM regstier 63.
            if (!intanChipPresent)
            {
                register59Value = -1;
                return -1;
            }
            else
            {
                register59Value = dataBlock.AuxData[stream][2, 23 + 1]; // Register 59
                return dataBlock.AuxData[stream][2, 19 + 1]; // chip ID (Register 63)
            }
        }

    }
}
