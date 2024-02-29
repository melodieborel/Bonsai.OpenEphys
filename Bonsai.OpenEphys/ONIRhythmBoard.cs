using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Rhythm.Net;

namespace Bonsai.OpenEphys
{
    class ONIRhythmBoard : IDisposable
    {
        enum RhythmRegisters
        {
            ENABLE = 0,
            MODE,
            MAX_TIMESTEP,
            CABLE_DELAY,
            AUXCMD_BANK_1,
            AUXCMD_BANK_2,
            AUXCMD_BANK_3,
            MAX_AUXCMD_INDEX_1,
            MAX_AUXCMD_INDEX_2,
            MAX_AUXCMD_INDEX_3,
            LOOP_AUXCMD_INDEX_1,
            LOOP_AUXCMD_INDEX_2,
            LOOP_AUXCMD_INDEX_3,
            DATA_STREAM_1_8_SEL,
            DATA_STREAM_9_16_SEL,
            DATA_STREAM_EN,
            EXTERNAL_FAST_SETTLE,
            EXTERNAL_DIGOUT_A,
            EXTERNAL_DIGOUT_B,
            EXTERNAL_DIGOUT_C,
            EXTERNAL_DIGOUT_D,
            SYNC_CLKOUT_DIVIDE,
            DAC_CTL,
            DAC_SEL_1,
            DAC_SEL_2,
            DAC_SEL_3,
            DAC_SEL_4,
            DAC_SEL_5,
            DAC_SEL_6,
            DAC_SEL_7,
            DAC_SEL_8,
            DAC_THRESH_1,
            DAC_THRESH_2,
            DAC_THRESH_3,
            DAC_THRESH_4,
            DAC_THRESH_5,
            DAC_THRESH_6,
            DAC_THRESH_7,
            DAC_THRESH_8,
            HPF,
            SPI_RUNNING
        };

        public enum BoardMemState
        {
            BOARDMEM_INIT = 0,
            BOARDMEM_OK = 1,
            BOARDMEM_INVALID = 2, //this should not happen
            BOARDMEM_ERR = 3
        };

        enum RhythmMode
        {
            SPI_RUN_CONTINUOUS = 1,
            DSP_SETTLE = 2,
            TTL_OUT_MODE = 3,
            LED_ENABLE = 4
        };

        /** ONI device indices*/
        const uint DEVICE_RHYTHM = 0x0101;
        const uint DEVICE_TTL = 0x0102;
        const uint DEVICE_DAC = 0x0103;
        const uint DEVICE_MEM = 0x0001;

        const uint RHYTHM_HUB_MANAGER = 0x01FE;
        const uint HUB_CLOCK_SEL = 0x2000;
        const uint HUB_CLOCK_BUSY = 0x2001;

        enum MemRegisters
        {
            ENABLE = 0,
            CLK_DIV,
            CLK_HZ,
            TOTAL_MEM
        }


        public const int MAX_DATA_STREAMS = 16;

        oni.Context ctx;
        AmplifierSampleRate sampleRate;
        int numDataStreams; // total number of data streams currently enabled
        int[] dataStreamEnabled = new int[MAX_DATA_STREAMS]; // 0 (disabled) or 1 (enabled)
        int[] cableDelay = new int[4];

        const int blockReadSize = 24 * 1024;
        const int blockWriteSize = 2048;
        readonly int boardIndex;

        private void InitCtx()
        {
            ctx = new oni.Context("ft600", boardIndex);

            // Set read pre-allocation size
            ctx.BlockReadSize = blockReadSize;
            ctx.BlockWriteSize = blockWriteSize;
        }

        public ONIRhythmBoard(int boardIndex)
        {
            this.boardIndex = boardIndex;
            InitCtx();

            int i;
            sampleRate = AmplifierSampleRate.SampleRate30000Hz; // Rhythm FPGA boots up with 30.0 kS/s/channel sampling rate
            numDataStreams = 0;

            for (i = 0; i < MAX_DATA_STREAMS; ++i)
            {
                dataStreamEnabled[i] = 0;
            }
        }

        public void Initialize()
        {
            int i;

           //Board must be manually reset before this call
           
            SetSampleRate(AmplifierSampleRate.SampleRate30000Hz);
            SelectAuxCommandBank(BoardPort.PortA, AuxCmdSlot.AuxCmd1, 0);
            SelectAuxCommandBank(BoardPort.PortB, AuxCmdSlot.AuxCmd1, 0);
            SelectAuxCommandBank(BoardPort.PortC, AuxCmdSlot.AuxCmd1, 0);
            SelectAuxCommandBank(BoardPort.PortD, AuxCmdSlot.AuxCmd1, 0);
            SelectAuxCommandBank(BoardPort.PortA, AuxCmdSlot.AuxCmd2, 0);
            SelectAuxCommandBank(BoardPort.PortB, AuxCmdSlot.AuxCmd2, 0);
            SelectAuxCommandBank(BoardPort.PortC, AuxCmdSlot.AuxCmd2, 0);
            SelectAuxCommandBank(BoardPort.PortD, AuxCmdSlot.AuxCmd2, 0);
            SelectAuxCommandBank(BoardPort.PortA, AuxCmdSlot.AuxCmd3, 0);
            SelectAuxCommandBank(BoardPort.PortB, AuxCmdSlot.AuxCmd3, 0);
            SelectAuxCommandBank(BoardPort.PortC, AuxCmdSlot.AuxCmd3, 0);
            SelectAuxCommandBank(BoardPort.PortD, AuxCmdSlot.AuxCmd3, 0);
            SelectAuxCommandLength(AuxCmdSlot.AuxCmd1, 0, 0);
            SelectAuxCommandLength(AuxCmdSlot.AuxCmd2, 0, 0);
            SelectAuxCommandLength(AuxCmdSlot.AuxCmd3, 0, 0);
            SetContinuousRunMode(true);
            SetMaxTimeStep(4294967295);  // 4294967295 == (2^32 - 1)

            SetCableLengthFeet(BoardPort.PortA, 3.0);  // assume 3 ft cables
            SetCableLengthFeet(BoardPort.PortB, 3.0);
            SetCableLengthFeet(BoardPort.PortC, 3.0);
            SetCableLengthFeet(BoardPort.PortD, 3.0);

            SetDspSettle(false);

            SetDataSource(0, BoardDataSource.PortA1);
            SetDataSource(1, BoardDataSource.PortB1);
            SetDataSource(2, BoardDataSource.PortC1);
            SetDataSource(3, BoardDataSource.PortD1);
            SetDataSource(4, BoardDataSource.PortA2);
            SetDataSource(5, BoardDataSource.PortB2);
            SetDataSource(6, BoardDataSource.PortC2);
            SetDataSource(7, BoardDataSource.PortD2);
            SetDataSource(8, BoardDataSource.PortA1);
            SetDataSource(9, BoardDataSource.PortB1);
            SetDataSource(10, BoardDataSource.PortC1);
            SetDataSource(11, BoardDataSource.PortD1);
            SetDataSource(12, BoardDataSource.PortA2);
            SetDataSource(13, BoardDataSource.PortB2);
            SetDataSource(14, BoardDataSource.PortC2);
            SetDataSource(15, BoardDataSource.PortD2);

            EnableDataStream(0, true);        // start with only one data stream enabled
            for (i = 1; i < MAX_DATA_STREAMS; i++)
            {
                EnableDataStream(i, false);
            }
            UpdateStreamBlockSize();

            ClearTtlOut();

            EnableDac(0, false);
            EnableDac(1, false);
            EnableDac(2, false);
            EnableDac(3, false);
            EnableDac(4, false);
            EnableDac(5, false);
            EnableDac(6, false);
            EnableDac(7, false);
            SelectDacDataStream(0, 0);
            SelectDacDataStream(1, 0);
            SelectDacDataStream(2, 0);
            SelectDacDataStream(3, 0);
            SelectDacDataStream(4, 0);
            SelectDacDataStream(5, 0);
            SelectDacDataStream(6, 0);
            SelectDacDataStream(7, 0);
            SelectDacDataChannel(0, 0);
            SelectDacDataChannel(1, 0);
            SelectDacDataChannel(2, 0);
            SelectDacDataChannel(3, 0);
            SelectDacDataChannel(4, 0);
            SelectDacDataChannel(5, 0);
            SelectDacDataChannel(6, 0);
            SelectDacDataChannel(7, 0);

            SetDacManual(32768);    // midrange value = 0 V

            SetDacGain(0);
            SetAudioNoiseSuppress(0);

            SetTtlMode(1);          // Digital outputs 0-7 are DAC comparators; 8-15 under manual control

            SetDacThreshold(0, 32768, true);
            SetDacThreshold(1, 32768, true);
            SetDacThreshold(2, 32768, true);
            SetDacThreshold(3, 32768, true);
            SetDacThreshold(4, 32768, true);
            SetDacThreshold(5, 32768, true);
            SetDacThreshold(6, 32768, true);
            SetDacThreshold(7, 32768, true);

            EnableExternalFastSettle(false);
            SetExternalFastSettleChannel(0);

            EnableExternalDigOut(BoardPort.PortA, false);
            EnableExternalDigOut(BoardPort.PortB, false);
            EnableExternalDigOut(BoardPort.PortC, false);
            EnableExternalDigOut(BoardPort.PortD, false);
            SetExternalDigOutChannel(BoardPort.PortA, 0);
            SetExternalDigOutChannel(BoardPort.PortB, 0);
            SetExternalDigOutChannel(BoardPort.PortC, 0);
            SetExternalDigOutChannel(BoardPort.PortD, 0);
        }

        ///<summary>
        ///Sets the board LED state
        ///</summary>
        public void SetBoardLeds(bool enable)
        {
            uint val = enable ? (uint)(1 << (int)RhythmMode.LED_ENABLE) : 0;
            WriteRegMask(DEVICE_RHYTHM, (uint)RhythmRegisters.MODE, val, (1 << (int)RhythmMode.LED_ENABLE));
        }

        /// <summary>
        /// Sets the per-channel sampling rate of the RHD2000 chips connected to the Rhythm FPGA.
        /// </summary>
        /// <param name="newSampleRate">The new per-channel sampling rate for RHD2000 chips connected to the Rhythm FPGA.</param>
        public void SetSampleRate(AmplifierSampleRate newSampleRate)
        {
            /*
            * Sample rate in the new board is controlled by a register in the Rhythm hub manager.
            * To control it, 3 main clocks are created through PLLs. Clock can be chosen from one
            * of these and, optionally, be devided by any even number.
            * To access the register, device address is 510 register 8192. Its value is as follows:
            * Bit0: 0 = Clock directly from one of the PLL outputs. 1 = Clock will be divided
            * Bits1-2: "00" or "11" 84Mhz clock for 30KS/s. "01" 70MHz Clock for 25KS/s. "10" 56MHz clock for 20KS/s
            * Bits3-6: if bit 0 is "1" the selected clock will be divided by this 2*(value + 1) (i.e.: 0 is a /2)
            */
            uint divEn;
            uint clockSel;
            uint divider;
            switch (newSampleRate)
            {
                case AmplifierSampleRate.SampleRate1000Hz:
                    divEn = 1;
                    clockSel = 2;
                    divider = 9;
                    break;
                case AmplifierSampleRate.SampleRate1250Hz:
                    divEn = 1;
                    clockSel = 1;
                    divider = 9;
                    break;
                case AmplifierSampleRate.SampleRate1500Hz:
                    divEn = 1;
                    clockSel = 0;
                    divider = 9;
                    break;
                case AmplifierSampleRate.SampleRate2000Hz:
                    divEn = 1;
                    clockSel = 2;
                    divider = 4;
                    break;
                case AmplifierSampleRate.SampleRate2500Hz:
                    divEn = 1;
                    clockSel = 1;
                    divider = 4;
                    break;
                case AmplifierSampleRate.SampleRate3000Hz:
                    divEn = 1;
                    clockSel = 0;
                    divider = 4;
                    break;
                case AmplifierSampleRate.SampleRate3333Hz:
                    divEn = 1;
                    clockSel = 2;
                    divider = 2;
                    break;
                case AmplifierSampleRate.SampleRate5000Hz:
                    divEn = 1;
                    clockSel = 2;
                    divider = 1;
                    break;
                case AmplifierSampleRate.SampleRate6250Hz:
                    divEn = 1;
                    clockSel = 1;
                    divider = 1;
                    break;
                case AmplifierSampleRate.SampleRate10000Hz:
                    divEn = 1;
                    clockSel = 2;
                    divider = 0;
                    break;
                case AmplifierSampleRate.SampleRate12500Hz:
                    divEn = 1;
                    clockSel = 1;
                    divider = 0;
                    break;
                case AmplifierSampleRate.SampleRate15000Hz:
                    divEn = 1;
                    clockSel = 0;
                    divider = 0;
                    break;
                case AmplifierSampleRate.SampleRate20000Hz:
                    divEn = 0;
                    clockSel = 2;
                    divider = 0;
                    break;
                case AmplifierSampleRate.SampleRate25000Hz:
                    divEn = 0;
                    clockSel = 1;
                    divider = 0;
                    break;
                case AmplifierSampleRate.SampleRate30000Hz:
                    divEn = 0;
                    clockSel = 0;
                    divider = 0;
                    break;
                default:
                    throw new ArgumentException("Unsupported amplifier sampling rate.", "newSampleRate");
            }
            uint val = ((divider & 0xF) << 3) + ((clockSel & 0x3) << 1) + (divEn & 0x1);
            ctx.WriteRegister(RHYTHM_HUB_MANAGER, HUB_CLOCK_SEL, val);
            do
            {
                val = ctx.ReadRegister(RHYTHM_HUB_MANAGER, HUB_CLOCK_BUSY);
            } while (val != 0);

            sampleRate = newSampleRate;
        }

        /// <summary>
        /// Returns the current per-channel sampling rate (in Hz) as a floating-point number.
        /// </summary>
        /// <returns>The current per-channel sampling rate (in Hz) as a floating-point number.</returns>
        public double GetSampleRate()
        {
            switch (sampleRate)
            {
                case AmplifierSampleRate.SampleRate1000Hz:
                    return 1000.0;
                case AmplifierSampleRate.SampleRate1250Hz:
                    return 1250.0;
                case AmplifierSampleRate.SampleRate1500Hz:
                    return 1500.0;
                case AmplifierSampleRate.SampleRate2000Hz:
                    return 2000.0;
                case AmplifierSampleRate.SampleRate2500Hz:
                    return 2500.0;
                case AmplifierSampleRate.SampleRate3000Hz:
                    return 3000.0;
                case AmplifierSampleRate.SampleRate3333Hz:
                    return (10000.0 / 3.0);
                case AmplifierSampleRate.SampleRate5000Hz:
                    return 5000.0;
                case AmplifierSampleRate.SampleRate6250Hz:
                    return 6250.0;
                case AmplifierSampleRate.SampleRate10000Hz:
                    return 10000.0;
                case AmplifierSampleRate.SampleRate12500Hz:
                    return 12500.0;
                case AmplifierSampleRate.SampleRate15000Hz:
                    return 15000.0;
                case AmplifierSampleRate.SampleRate20000Hz:
                    return 20000.0;
                case AmplifierSampleRate.SampleRate25000Hz:
                    return 25000.0;
                case AmplifierSampleRate.SampleRate30000Hz:
                    return 30000.0;
                default:
                    return -1.0;
            }
        }

        /// <summary>
        /// Gets the current per-channel sampling rate as an <see cref="AmplifierSampleRate"/> enumeration.
        /// </summary>
        /// <returns>The current per-channel sampling rate as an <see cref="AmplifierSampleRate"/> enumeration.</returns>
        public AmplifierSampleRate GetSampleRateEnum()
        {
            return sampleRate;
        }

        /// <summary>
        /// Uploads a command list (generated by an instance of the <see cref="Rhd2000Registers"/> class) to a particular auxiliary command slot and
        /// RAM bank (0-15) on the FPGA.
        /// </summary>
        /// <param name="commandList">A command list generated by an instance of the <see cref="Rhd2000Registers"/> class.</param>
        /// <param name="auxCommandSlot">The auxiliary command slot on which to upload the command list.</param>
        /// <param name="bank">The RAM bank (0-15) on which to upload the command list.</param>
        public void UploadCommandList(List<int> commandList, AuxCmdSlot auxCommandSlot, uint bank)
        {
            if (auxCommandSlot != AuxCmdSlot.AuxCmd1 && auxCommandSlot != AuxCmdSlot.AuxCmd2 && auxCommandSlot != AuxCmdSlot.AuxCmd3)
            {
                throw new ArgumentException("auxCommandSlot out of range.", "auxCommandSlot");
            }

            if (bank < 0 || bank > 15)
            {
                throw new ArgumentException("bank out of range.", "bank");
            }

            uint base_address = 0x4000;

            uint bank_select = (bank << 10);

            uint aux_select;
            switch (auxCommandSlot)
            {
                case AuxCmdSlot.AuxCmd1:
                    aux_select = (0 << 14);
                    break;
                case AuxCmdSlot.AuxCmd2:
                    aux_select = (1 << 14);
                    break;
                case AuxCmdSlot.AuxCmd3:
                    aux_select = (2 << 14);
                    break;
                default:
                    aux_select = 0;
                    break;
            }
            for (uint i = 0; i < commandList.Count; ++i)
            {
                ctx.WriteRegister(DEVICE_RHYTHM, base_address + bank_select + aux_select + i, (uint)commandList[(int)i]);
            }
        }

        // <summary>
        /// Selects an auxiliary command slot (AuxCmd1, AuxCmd2, or AuxCmd3) and bank (0-15) for a particular SPI port.
        /// </summary>
        /// <param name="port">The SPI port on which the auxiliary command slot is selected.</param>
        /// <param name="auxCommandSlot">The auxiliary command slot to be selected.</param>
        /// <param name="bank">The RAM bank (0-15) to be selected.</param>
        public void SelectAuxCommandBank(BoardPort port, AuxCmdSlot auxCommandSlot, int bank)
        {
            int bitShift;

            if (auxCommandSlot != AuxCmdSlot.AuxCmd1 && auxCommandSlot != AuxCmdSlot.AuxCmd2 && auxCommandSlot != AuxCmdSlot.AuxCmd3)
            {
                throw new ArgumentException("auxCommandSlot out of range.", "auxCommandSlot");
            }

            if (bank < 0 || bank > 15)
            {
                throw new ArgumentException("bank out of range.", "bank");
            }

            switch (port)
            {
                case BoardPort.PortA:
                    bitShift = 0;
                    break;
                case BoardPort.PortB:
                    bitShift = 4;
                    break;
                case BoardPort.PortC:
                    bitShift = 8;
                    break;
                case BoardPort.PortD:
                    bitShift = 12;
                    break;
                default:
                    throw new ArgumentException("port out of range.", "port");
            }

            switch (auxCommandSlot)
            {
                case AuxCmdSlot.AuxCmd1:
                    WriteRegMask(DEVICE_RHYTHM, (uint)RhythmRegisters.AUXCMD_BANK_1, (uint)(bank << bitShift), (uint)(0x000f << bitShift));
                    break;
                case AuxCmdSlot.AuxCmd2:
                    WriteRegMask(DEVICE_RHYTHM, (uint)RhythmRegisters.AUXCMD_BANK_2, (uint)(bank << bitShift), (uint)(0x000f << bitShift));
                    break;
                case AuxCmdSlot.AuxCmd3:
                    WriteRegMask(DEVICE_RHYTHM, (uint)RhythmRegisters.AUXCMD_BANK_3, (uint)(bank << bitShift), (uint)(0x000f << bitShift));
                    break;
            }
        }

        /// <summary>
        /// Specifies a command sequence end point (endIndex = 0-1023) and command loop index (loopIndex = 0-1023) for a particular
        /// auxiliary command slot (AuxCmd1, AuxCmd2, or AuxCmd3).
        /// </summary>
        /// <param name="auxCommandSlot">The auxiliary command slot on which to specify the command sequence length.</param>
        /// <param name="loopIndex">The command sequence loop index (0-1023).</param>
        /// <param name="endIndex">The command sequence end point index (0-1023).</param>
        public void SelectAuxCommandLength(AuxCmdSlot auxCommandSlot, int loopIndex, int endIndex)
        {
            if (auxCommandSlot != AuxCmdSlot.AuxCmd1 && auxCommandSlot != AuxCmdSlot.AuxCmd2 && auxCommandSlot != AuxCmdSlot.AuxCmd3)
            {
                throw new ArgumentException("auxCommandSlot out of range.", "auxCommandSlot");
            }

            if (loopIndex < 0 || loopIndex > 1023)
            {
                throw new ArgumentException("loopIndex out of range.", "loopIndex");
            }

            if (endIndex < 0 || endIndex > 1023)
            {
                throw new ArgumentException("endIndex out of range.", "endIndex");
            }

            switch (auxCommandSlot)
            {
                case AuxCmdSlot.AuxCmd1:
                    ctx.WriteRegister(DEVICE_RHYTHM, (uint)RhythmRegisters.LOOP_AUXCMD_INDEX_1, (uint)loopIndex);
                    ctx.WriteRegister(DEVICE_RHYTHM, (uint)RhythmRegisters.MAX_AUXCMD_INDEX_1, (uint)endIndex);
                    break;
                case AuxCmdSlot.AuxCmd2:
                    ctx.WriteRegister(DEVICE_RHYTHM, (uint)RhythmRegisters.LOOP_AUXCMD_INDEX_2, (uint)loopIndex);
                    ctx.WriteRegister(DEVICE_RHYTHM, (uint)RhythmRegisters.MAX_AUXCMD_INDEX_2, (uint)endIndex);
                    break;
                case AuxCmdSlot.AuxCmd3:
                    ctx.WriteRegister(DEVICE_RHYTHM, (uint)RhythmRegisters.LOOP_AUXCMD_INDEX_3, (uint)loopIndex);
                    ctx.WriteRegister(DEVICE_RHYTHM, (uint)RhythmRegisters.MAX_AUXCMD_INDEX_3, (uint)endIndex);
                    break;
            }
        }

        public void ResetBoard()
        {
            //This should really be just a refresh of the device map, but clroni does not allow it
            ctx?.Dispose();
            InitCtx();
            //ctx.Refresh(); //seems to trigger a firmware bug with the memory usage device at the moment
        }

        /// <summary>
        /// Sets the FPGA to run continuously once started (if continuousMode is set to true) or to run until
        /// maxTimeStep is reached (if continuousMode is set to false).
        /// </summary>
        /// <param name="continuousMode">
        /// Set the FPGA to run continuously once started if set to true or to run until
        /// maxTimeStep is reached if set to false.
        /// </param>
        public void SetContinuousRunMode(bool continuousMode)
        {
            uint val = continuousMode ? (uint)(1 << (int)RhythmMode.SPI_RUN_CONTINUOUS) : 0;
            WriteRegMask(DEVICE_RHYTHM, (uint)RhythmRegisters.MODE, val, (1 << (int)RhythmMode.SPI_RUN_CONTINUOUS));
        }

        /// <summary>
        /// Sets maxTimeStep for cases where continuousMode is set to false.
        /// </summary>
        /// <param name="maxTimeStep">
        /// The maxTimeStep (in number of samples) for which to run the
        /// interface when continuousMode is set to false.
        /// </param>
        public void SetMaxTimeStep(uint maxTimeStep)
        {
            ctx.WriteRegister(DEVICE_RHYTHM, (uint)RhythmRegisters.MAX_TIMESTEP, maxTimeStep);
        }

        /// <summary>
        /// Starts SPI data acquisition.
        /// </summary>
        public void Run()
        {
            ctx.Start(true);
        }

        /// <summary>
        ///  Stops acquisition
        /// </summary>
        public void Stop()
        {
            ctx.Stop();
        }

        /// <summary>
        /// Returns true if the FPGA is currently running SPI data acquisition.
        /// </summary>
        /// <returns>True if the FPGA is currently running SPI data acquisition, false otherwise.</returns>
        public bool IsRunning()
        {
            uint value = ctx.ReadRegister(DEVICE_RHYTHM, (uint)RhythmRegisters.SPI_RUNNING);
            return value != 0;
        }

        /// <summary>
        /// Sets the delay for sampling the MISO line on a particular SPI port (PortA - PortD), in integer clock
        /// steps, where each clock step is 1/2800 of a per-channel sampling period.
        /// </summary>
        /// <param name="port">The SPI port for which to set the MISO line sampling delay.</param>
        /// <param name="delay">The delay for sampling the MISO line, in integer clock steps.</param>
        /// <remarks>
        /// Cable delay must be updated after any changes are made to the sampling rate, since cable delay
        /// calculations are based on the clock period.
        /// </remarks>
        public void SetCableDelay(BoardPort port, int delay)
        {
            int bitShift;

            if (delay < 0 || delay > 15)
            {
                Console.Error.WriteLine("Warning in Rhd2000EvalBoard.SetCableDelay: delay out of range.");
            }

            if (delay < 0) delay = 0;
            if (delay > 15) delay = 15;

            switch (port)
            {
                case BoardPort.PortA:
                    bitShift = 0;
                    cableDelay[0] = delay;
                    break;
                case BoardPort.PortB:
                    bitShift = 4;
                    cableDelay[1] = delay;
                    break;
                case BoardPort.PortC:
                    bitShift = 8;
                    cableDelay[2] = delay;
                    break;
                case BoardPort.PortD:
                    bitShift = 12;
                    cableDelay[3] = delay;
                    break;
                default:
                    throw new ArgumentException("port out of range.", "port");
            }

            WriteRegMask(DEVICE_RHYTHM, (uint)RhythmRegisters.CABLE_DELAY, (uint)(delay << bitShift), (uint)(0x000f << bitShift));
        }

        /// <summary>
        /// Sets the delay for sampling the MISO line on a particular SPI port (PortA - PortD) based on the length
        /// of the cable between the FPGA and the RHD2000 chip (in meters).
        /// </summary>
        /// <param name="port">The SPI port for which to set the MISO line sampling delay.</param>
        /// <param name="lengthInMeters">The length of the cable between the FPGA and the RHD2000 chip (in meters).</param>
        /// <remarks>
        /// Cable delay must be updated after any changes are made to the sampling rate, since cable delay
        /// calculations are based on the clock period.
        /// </remarks>
        public void SetCableLengthMeters(BoardPort port, double lengthInMeters)
        {
            int delay;
            double tStep, cableVelocity, distance, timeDelay;
            const double speedOfLight = 299792458.0;  // units = meters per second
            const double xilinxLvdsOutputDelay = 1.9e-9;    // 1.9 ns Xilinx LVDS output pin delay
            const double xilinxLvdsInputDelay = 1.4e-9;     // 1.4 ns Xilinx LVDS input pin delay
            const double rhd2000Delay = 9.0e-9;             // 9.0 ns RHD2000 SCLK-to-MISO delay
            const double misoSettleTime = 6.7e-9;          // 6.7 ns delay after MISO changes, before we sample it

            tStep = 1.0 / (2800.0 * GetSampleRate());  // data clock that samples MISO has a rate 35 x 80 = 2800x higher than the sampling rate
            cableVelocity = 0.555 * speedOfLight;  // propogation velocity on cable: improvement based on cable measurements
            distance = 2.0 * lengthInMeters;      // round trip distance data must travel on cable
            timeDelay = (distance / cableVelocity) + xilinxLvdsOutputDelay + rhd2000Delay + xilinxLvdsInputDelay + misoSettleTime;

            delay = (int)Math.Floor(((timeDelay / tStep) + 1.0) + 0.5);

            if (delay < 1) delay = 1;   // delay of zero is too short (due to I/O delays), even for zero-length cables

            SetCableDelay(port, delay);
        }

        /// <summary>
        /// Sets the delay for sampling the MISO line on a particular SPI port (PortA - PortD) based on the length
        /// of the cable between the FPGA and the RHD2000 chip (in feet).
        /// </summary>
        /// <param name="port">The SPI port for which to set the MISO line sampling delay.</param>
        /// <param name="lengthInFeet">The length of the cable between the FPGA and the RHD2000 chip (in feet).</param>
        /// <remarks>
        /// Cable delay must be updated after any changes are made to the sampling rate, since cable delay
        /// calculations are based on the clock period.
        /// </remarks>
        public void SetCableLengthFeet(BoardPort port, double lengthInFeet)
        {
            SetCableLengthMeters(port, 0.3048 * lengthInFeet);   // convert feet to meters
        }

        /// <summary>
        /// Estimates the cable length (in meters) between the FPGA and the RHD2000 chip based on a particular delay
        /// used in setCableDelay and the current sampling rate.
        /// </summary>
        /// <param name="delay">The delay for sampling the MISO line, in integer clock steps.</param>
        /// <returns>The estimated cable length (in meters) between the FPGA and the RHD2000 chip.</returns>
        public double EstimateCableLengthMeters(int delay)
        {
            double tStep, cableVelocity, distance;
            const double speedOfLight = 299792458.0;  // units = meters per second
            const double xilinxLvdsOutputDelay = 1.9e-9;    // 1.9 ns Xilinx LVDS output pin delay
            const double xilinxLvdsInputDelay = 1.4e-9;     // 1.4 ns Xilinx LVDS input pin delay
            const double rhd2000Delay = 9.0e-9;             // 9.0 ns RHD2000 SCLK-to-MISO delay
            const double misoSettleTime = 6.7e-9;          // 6.7 ns delay after MISO changes, before we sample it

            tStep = 1.0 / (2800.0 * GetSampleRate());  // data clock that samples MISO has a rate 35 x 80 = 2800x higher than the sampling rate
            cableVelocity = 0.555 * speedOfLight;  // propogation velocity on cable: improvement based on cable measurements

            distance = cableVelocity * ((((double)delay) - 1.0) * tStep - (xilinxLvdsOutputDelay + rhd2000Delay + xilinxLvdsInputDelay + misoSettleTime));
            if (distance < 0.0) distance = 0.0;

            return (distance / 2.0);
        }

        /// <summary>
        /// Estimates the cable length (in feet) between the FPGA and the RHD2000 chip based on a particular delay
        /// used in setCableDelay and the current sampling rate.
        /// </summary>
        /// <param name="delay">The delay for sampling the MISO line, in integer clock steps.</param>
        /// <returns>The estimated cable length (in feet) between the FPGA and the RHD2000 chip.</returns>
        public double EstimateCableLengthFeet(int delay)
        {
            return 3.2808 * EstimateCableLengthMeters(delay);
        }

        /// <summary>
        /// Turns on or off the DSP settle function in the FPGA. This only executes when CONVERT commands are executed
        /// by the RHD2000.
        /// </summary>
        /// <param name="enabled">Turns on DSP settle if set to true, turns it off otherwise.</param>
        public void SetDspSettle(bool enabled)
        {
            uint val = enabled ? (uint)(1 << (int)RhythmMode.DSP_SETTLE) : 0;
            WriteRegMask(DEVICE_RHYTHM, (uint)RhythmRegisters.MODE, val, 1 << (int)RhythmMode.DSP_SETTLE);
        }

        /// <summary>
        /// Assigns a particular data source (e.g., PortA1, PortA2, PortB1,...) to one of the eight
        /// available USB data streams (0-7).
        /// </summary>
        /// <param name="stream">The USB data stream (0-7) for which to assign the data source.</param>
        /// <param name="dataSource">
        /// The particular data source (e.g., PortA1, PortA2, PortB1,...) to assign
        /// to one of the available USB data streams.
        /// </param>
        public void SetDataSource(int stream, BoardDataSource dataSource)
        {
            if (stream < 0 || stream > (MAX_DATA_STREAMS - 1)) throw new ArgumentException("stream out of range.", "stream");
            uint reg = (uint)RhythmRegisters.DATA_STREAM_1_8_SEL + (uint)(stream / 8);
            int bitShift = 4 * (stream % 8);
            WriteRegMask(DEVICE_RHYTHM, reg, (uint)dataSource << (int)bitShift, (uint)(0x000f << bitShift));
        }

        /// <summary>
        /// Enables or disables one of the eight available USB data streams (0-7).
        /// </summary>
        /// <param name="stream">The USB data stream (0-7) to enable or disable.</param>
        /// <param name="enabled">Enables the USB data stream if set to true or disables it if set to false.</param>
        public void EnableDataStream(int stream, bool enabled)
        {
            if (stream < 0 || stream > (MAX_DATA_STREAMS - 1))
            {
                throw new ArgumentException("stream out of range.", "stream");
            }

            if (enabled)
            {
                if (dataStreamEnabled[stream] == 0)
                {
                    WriteRegMask(DEVICE_RHYTHM,(uint)RhythmRegisters.DATA_STREAM_EN, (uint)(0x0001 << stream), (uint)(0x0001 << stream));
                    dataStreamEnabled[stream] = 1;
                    ++numDataStreams;
                }
            }
            else
            {
                if (dataStreamEnabled[stream] == 1)
                {
                    WriteRegMask(DEVICE_RHYTHM, (uint)RhythmRegisters.DATA_STREAM_EN, (uint)(0x0000 << stream), (uint)(0x0001 << stream));
                    dataStreamEnabled[stream] = 0;
                    numDataStreams--;
                }
            }
        }

        /// <summary>
        /// Returns the number of enabled USB data streams.
        /// </summary>
        /// <returns>The number of enabled USB data streams.</returns>
        public int GetNumEnabledDataStreams()
        {
            return numDataStreams;
        }

        public void UpdateStreamBlockSize()
        {
            //At this moment, we can only change device transfer sizes after a reset. In the future, we might
            //implement dynamic frames, so this could change.
            ResetBoard();
        }

        /// <summary>
        /// Sets the 16 bits of the digital TTL output lines on the FPGA.
        /// </summary>
        /// <param name="value">
        /// The 16-bit value (0-65536) to which the digital TTL output lines will be set.
        /// </param>
        public void SetTtlOut(int value)
        {
            if (value < 0 || value > 65535)
            {
                throw new ArgumentException("value out of range.", "value");
            }
            uint[] val = new uint[1];
            val[0] = (uint)value;

            ctx.Write(DEVICE_TTL, val);
;        }

        /// <summary>
        /// Sets all 16 bits of the digital TTL output lines on the FPGA to zero.
        /// </summary>
        public void ClearTtlOut()
        {
            SetTtlOut(0);
        }

        /// <summary>
        /// Sets the 16 bits of the digital TTL output lines on the FPGA high or low according to an integer array.
        /// </summary>
        /// <param name="ttlOutArray">
        /// A length-16 array containing values of 0 or 1 to specify high or low bits in the TTL output lines.
        /// </param>
        public void SetTtlOut(int[] ttlOutArray)
        {
            int i, ttlOut;

            ttlOut = 0;
            for (i = 0; i < 16; ++i)
            {
                if (ttlOutArray[i] > 0)
                    ttlOut += 1 << i;
            }
            SetTtlOut(ttlOut);
        }

        /// <summary>
        /// Sets the manual AD5662 DAC control WireIns to the specified value (0-65536).
        /// </summary>
        /// <param name="value">
        /// The 16-bit value (0-65536) to which the manual DAC control WireIns will be set.
        /// </param>
        public void SetDacManual(int value)
        {
            if (value < 0 || value > 65535)
            {
                throw new ArgumentException("value out of range.", "value");
            }

            UInt32 val = (uint)(value & 0xFFFF);
            UInt32[] values = new UInt32[4];
            for (int i = 0; i < 4; i++)
            {
                values[i] = val + (val << 16);
            }

            ctx.Write(DEVICE_TTL, values);
        }

        /// <summary>
        /// Enables or disables the AD5662 DACs connected to the FPGA.
        /// </summary>
        /// <param name="dacChannel">The AD5662 DAC channel (0-7) to enable or disable.</param>
        /// <param name="enabled">Enables the channel if set to true or disables it if set to false.</param>
        public void EnableDac(int dacChannel, bool enabled)
        {
            if (dacChannel < 0 || dacChannel > 7)
            {
                throw new ArgumentException("dacChannel out of range.", "dacChannel");
            }
            uint reg = (uint)RhythmRegisters.DAC_SEL_1 + (uint)dacChannel;
            uint val = enabled ? (uint)0x0400 : 0;
            WriteRegMask(DEVICE_RHYTHM, reg, val, (uint)0x0400);
        }

        /// <summary>
        /// Scales the digital signals to all eight AD5662 DACs by a factor of 2^<paramref name="gain"/>.
        /// </summary>
        /// <param name="gain">A number between 0 and 7 indicating the power of two by which to scale digital signals.</param>
        public void SetDacGain(int gain)
        {
            if (gain < 0 || gain > 7)
            {
                throw new ArgumentException("gain out of range.", "gain");
            }
            WriteRegMask(DEVICE_RHYTHM, (uint)RhythmRegisters.DAC_CTL, (uint)gain << 7, (uint)0x07 << 7);
        }

        /// <summary>
        /// Sets the noise slicing region for DAC channels 1 and 2 (i.e., audio left and right) to +/-16*<paramref name="noiseSuppress"/> LSBs,
        /// where noiseSuppress is between 0 and 127. This improves the audibility of weak neural spikes in noisy waveforms.
        /// </summary>
        /// <param name="noiseSuppress">A number between 0 and 127 specifying the audio noise suppression factor.</param>
        public void SetAudioNoiseSuppress(int noiseSuppress)
        {
            if (noiseSuppress < 0 || noiseSuppress > 127)
            {
                throw new ArgumentException("noiseSuppress out of range.", "noiseSuppress");
            }

            WriteRegMask(DEVICE_RHYTHM, (uint)RhythmRegisters.DAC_CTL, (uint)noiseSuppress, (uint)0x7F);
        }

        /// <summary>
        /// Assigns a particular data stream (0-7) to an AD5662 DAC channel (0-7). Setting stream
        /// to 8 selects DacManual1 value; setting stream to 9 selects DacManual2 value.
        /// </summary>
        /// <param name="dacChannel">The DAC channel to which the data stream will be assigned.</param>
        /// <param name="stream">The data stream to assign to the DAC channel.</param>
        public void SelectDacDataStream(int dacChannel, int stream)
        {
            if (dacChannel < 0 || dacChannel > 7)
            {
                throw new ArgumentException("dacChannel out of range.", "dacChannel");
            }

            if (stream < 0 || stream > MAX_DATA_STREAMS + 1)
            {
                throw new ArgumentException("stream out of range.", "stream");
            }

            uint reg = (uint)RhythmRegisters.DAC_SEL_1 + (uint)dacChannel;
            uint val = (uint)stream << 5;
            WriteRegMask(DEVICE_RHYTHM, reg, val, 0x1F << 5);

        }

        /// <summary>
        /// Assigns a particular amplifier channel (0-31) to an AD5662 DAC channel (0-7).
        /// </summary>
        /// <param name="dacChannel">The DAC channel to which the amplifier channel will be assigned.</param>
        /// <param name="dataChannel">The amplifier channel to assign to the DAC channel.</param>
        public void SelectDacDataChannel(int dacChannel, int dataChannel)
        {
            if (dacChannel < 0 || dacChannel > 7)
            {
                throw new ArgumentException("dacChannel out of range.", "dacChannel");
            }

            if (dataChannel < 0 || dataChannel > 31)
            {
                throw new ArgumentException("dataChannel out of range.", "dataChannel");
            }

            uint reg = (uint)RhythmRegisters.DAC_SEL_1 + (uint)dacChannel;
            uint val = (uint)dataChannel;
            WriteRegMask(DEVICE_RHYTHM, reg, val, 0x1F);
        }

        /// <summary>
        /// Enables or disables external triggering of amplifier hardware 'fast settle' function (blanking).
        /// </summary>
        /// <param name="enable">
        /// Enables external triggering of 'fast settle' function if set to true or disables it
        /// if set to false.
        /// </param>
        /// <remarks>
        /// If external triggering is enabled, the fast settling of amplifiers on all connected
        /// chips will be controlled in real time via one of the 16 TTL inputs.
        /// </remarks>
        public void EnableExternalFastSettle(bool enable)
        {
            uint val = enable ? (uint)1 << 4 : 0;
            WriteRegMask(DEVICE_RHYTHM,(uint)RhythmRegisters.EXTERNAL_FAST_SETTLE, val, 1 << 4);
        }

        /// <summary>
        /// Selects which of the TTL inputs 0-15 is used to perform a hardware 'fast settle' (blanking)
        /// of the amplifiers if external triggering of fast settling is enabled.
        /// </summary>
        /// <param name="channel">
        /// The TTL input channel used to trigger a 'fast settle' of all connected chips.
        /// </param>
        public void SetExternalFastSettleChannel(int channel)
        {
            if (channel < 0 || channel > 15)
            {
                throw new ArgumentException("channel out of range.", "channel");
            }

            WriteRegMask(DEVICE_RHYTHM, (uint)RhythmRegisters.EXTERNAL_FAST_SETTLE, (uint)channel, 0x0F);
        }

        /// <summary>
        /// Enables or disables external control of RHD2000 auxiliary digital output pin (auxout).
        /// </summary>
        /// <param name="port">
        /// The SPI port for which to enable or disable external control of RHD2000 auxiliary
        /// digital output pin.
        /// </param>
        /// <param name="enable">
        /// Enables external control of RHD2000 auxiliary digital output pin if set to true or
        /// disables it if set to false.
        /// </param>
        /// <remarks>
        /// If external control is enabled, the digital output of all chips connected to a
        /// selected SPI port will be controlled in real time via one of the 16 TTL inputs.
        /// </remarks>
        public void EnableExternalDigOut(BoardPort port, bool enable)
        {
            uint  reg = (uint)RhythmRegisters.EXTERNAL_DIGOUT_A + (uint)port;
            uint  val = enable ? (uint)1 << 4 : 0;
            WriteRegMask(DEVICE_RHYTHM, reg, val, 1 << 4);
        }

        /// <summary>
        /// Selects which of the TTL inputs 0-15 is used to control the auxiliary digital output
        /// pin of the chips connected to a particular SPI port, if external control of auxout is enabled.
        /// </summary>
        /// <param name="port">
        /// Specifies the SPI port where the controlled auxiliary digital output pins are connected.
        /// </param>
        /// <param name="channel">
        /// The TTL input channel used to control the auxiliary digital output pin of the chips connected
        /// to the specified SPI port.
        /// </param>
        public void SetExternalDigOutChannel(BoardPort port, int channel)
        {
            if (channel < 0 || channel > 15)
            {
                throw new ArgumentException("channel out of range.", "channel");
            }

            uint reg = (uint)RhythmRegisters.EXTERNAL_DIGOUT_A + (uint)port;
            uint val = (uint)channel;
            WriteRegMask(DEVICE_RHYTHM, reg, val, 0x0F);

        }

        /// <summary>
        /// Enables or disables optional FPGA-implemented digital high-pass filters associated with DAC
        /// outputs on USB interface board.
        /// </summary>
        /// <param name="enable">
        /// Enables optional high-pass filters associated with DAC outputs if set to true or disables
        /// them if set to false.
        /// </param>
        /// <remarks>
        /// These one-pole filters can be used to record wideband neural data while viewing only spikes
        /// without LFPs on the DAC outputs, for example.  This is useful when using the low-latency
        /// FPGA thresholds to detect spikes and produce digital pulses on the TTL outputs, for example.
        /// </remarks>
        public void EnableDacHighpassFilter(bool enable)
        {
            uint val = enable ? (uint)1 << 16 : 0;
            WriteRegMask(DEVICE_RHYTHM,(uint)RhythmRegisters.HPF, val, 1 << 16);
        }

        /// <summary>
        /// Sets cutoff frequency (in Hz) for optional FPGA-implemented digital high-pass filters
        /// associated with DAC outputs on USB interface board.
        /// </summary>
        /// <param name="cutoff">
        /// The cutoff frequency (in Hz) for the DAC output high-pass filters.
        /// </param>
        /// <remarks>
        /// These one-pole filters can be used to record wideband neural data while viewing only spikes
        /// without LFPs on the DAC outputs, for example.  This is useful when using the low-latency
        /// FPGA thresholds to detect spikes and produce digital pulses on the TTL outputs, for example.
        /// </remarks>
        public void SetDacHighpassFilter(double cutoff)
        {
            double b;
            int filterCoefficient;

            // Note that the filter coefficient is a function of the amplifier sample rate, so this
            // function should be called after the sample rate is changed.
            b = 1.0 - Math.Exp(-2.0 * Math.PI * cutoff / GetSampleRate());

            // In hardware, the filter coefficient is represented as a 16-bit number.
            filterCoefficient = (int)Math.Floor(65536.0 * b + 0.5);

            if (filterCoefficient < 1)
            {
                filterCoefficient = 1;
            }
            else if (filterCoefficient > 65535)
            {
                filterCoefficient = 65535;
            }

            WriteRegMask(DEVICE_RHYTHM, (uint)RhythmRegisters.HPF, (uint)filterCoefficient, 0xFFFF);
        }

        /// <summary>
        /// Sets thresholds for DAC channels; threshold output signals appear on TTL outputs 0-7.
        /// </summary>
        /// <param name="dacChannel"></param>
        /// <param name="threshold">
        /// The RHD2000 chip ADC output value, falling in the range of 0 to 65535, where the
        /// 'zero' level is 32768.
        /// </param>
        /// <param name="trigPolarity">
        /// If trigPolarity is true, voltages equaling or rising above the threshold produce a high TTL
        /// output; otherwise, voltages equaling or falling below the threshold produce a high TTL output.
        /// </param>
        public void SetDacThreshold(int dacChannel, int threshold, bool trigPolarity)
        {
            if (dacChannel < 0 || dacChannel > 7)
            {
                throw new ArgumentException("dacChannel out of range.", "dacChannel");
            }

            if (threshold < 0 || threshold > 65535)
            {
                throw new ArgumentException("threshold out of range.", "threshold");
            }

            uint reg = (uint)RhythmRegisters.DAC_THRESH_1 + (uint)dacChannel;
            uint val = (uint)((threshold & 0x0FFFF) + ((trigPolarity ? 1 : 0) << 16));

            ctx.WriteRegister(DEVICE_RHYTHM, reg, val);
        }

        /// <summary>
        /// Sets the TTL output mode of the board.
        /// </summary>
        /// <param name="mode">
        /// If set to 0, all 16 TTL outputs are under manual control; if set to 1,
        /// the top 8 TTL outputs are under manual control; while the bottom 8 TTL
        /// outputs are outputs of DAC comparators.
        /// </param>
        public void SetTtlMode(int mode)
        {
            if (mode < 0 || mode > 1)
            {
                throw new ArgumentException("mode out of range.", "mode");
            }

            WriteRegMask(DEVICE_RHYTHM,(uint)RhythmRegisters.MODE, (uint)mode << (int)RhythmMode.TTL_OUT_MODE, 1 << (int)RhythmMode.TTL_OUT_MODE);
        }

        public void setMemDevice(int bufferSize, out uint memoryWords)
        {
            //read total memory
            memoryWords = ctx.ReadRegister(DEVICE_MEM, (uint)MemRegisters.TOTAL_MEM);

            //read clock
            uint clkHz = ctx.ReadRegister(DEVICE_MEM, (uint)MemRegisters.CLK_HZ);

            double desiredHz = GetSampleRate() / bufferSize;
            uint div = (uint)(clkHz / desiredHz);

            //set divider
            ctx.WriteRegister(DEVICE_MEM, (uint)MemRegisters.CLK_DIV, div);

            //Enable memory device
            ctx.WriteRegister(DEVICE_MEM, (uint)MemRegisters.ENABLE, 1);
        }

        public RhythmData readData(int samples, CancellationToken cancel, ref uint lastMem)
        {
            uint sample = 0;
            RhythmData dataBlock = new RhythmData(numDataStreams, samples);
            while (sample < samples && !cancel.IsCancellationRequested)
            {
                oni.Frame frame = ctx.ReadFrame();
                if (frame.DeviceAddress == DEVICE_MEM)
                {
                    uint[] data = frame.GetData<uint>();
                    lastMem = data[2];
                }
                else if (frame.DeviceAddress == DEVICE_RHYTHM)
                {
                    ushort[] data = frame.GetData<ushort>();
                    dataBlock.fillFromSample(data, sample);
                    sample++;
                }
                frame.Dispose();
            }
            return dataBlock;
        }

        public void Dispose()
        {
            ctx?.Dispose();
            ctx = null;
            GC.SuppressFinalize(this);
        }

        public void WriteRegMask(uint dev_idx, uint addr, uint value, uint mask)
        {
            uint val = ctx.ReadRegister(dev_idx, addr);
            val = (val & ~mask) | (value & mask);
            ctx.WriteRegister(dev_idx, addr, val);
        }

        public uint GetGatewareVersion()
        {
            return ctx.ReadRegister(254, 2);
        }
    }
}
