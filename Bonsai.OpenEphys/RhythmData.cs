using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bonsai.OpenEphys
{
    public class RhythmData
    {
        UInt32[] timestamps;
        UInt16[] ttlInData;
        UInt16[] ttlOutData;
        UInt16[][,] auxData;
        UInt16[,] ephysData;
        UInt16[,] adcData;

        const int channelsPerStream = 32;
        const int adcChannels = 8;
        const int auxChannels = 3;
        readonly int nStreams;
        readonly int nSamples;

        const ulong RHD2000_HEADER_MAGIC_NUMBER = 0xc691199927021942;

        public RhythmData(int numDataStreams, int numSamples)
        {
            nStreams = numDataStreams;
            nSamples = numSamples;
            timestamps = new UInt32[numSamples];
            ttlInData = new UInt16[numSamples];
            ttlOutData = new UInt16[numSamples];
            ephysData = new UInt16[Math.Max(1, nStreams * channelsPerStream), numSamples]; //Add a min of 1 channel to avoid crashes if there are no headstages. Probably should find a better solution
            adcData = new UInt16[adcChannels, numSamples];
            Array.Resize(ref auxData, numDataStreams);
            for (int i = 0; i < nStreams; i++)
            {
                auxData[i] = new UInt16[auxChannels, numSamples];
            }
        }

        public UInt32[] Timestamps
        {
            get { return timestamps; }
        }

        public UInt16[] TtlInData
        {
            get { return ttlInData; }
        }

        public UInt16[] TtlOutData
        {
            get { return ttlInData; }
        }

        public UInt16[][,] AuxData
        {
            get { return auxData; }
        }

        public UInt16[,] EphysData
        {
            get { return ephysData; }
        }

        public UInt16[,] AdcData
        {
            get { return adcData; }
        }

        public void fillFromSample(UInt16[] data, uint sample)
        {
            int index = 4; //Skip ONI timestamps

            if (!CheckUsbHeader(data, index))
            {
                throw new ArgumentException("Incorrect header.", "usbBuffer");
            }
            index += 4;
            timestamps[sample] = (uint)data[index] + ((uint)data[index + 1] << 16);
            index += 2;

            for (int channel = 0; channel < auxChannels; channel++)
            {
                for (int stream = 0; stream < nStreams; stream++)
                {
                    auxData[stream][channel, sample] = data[index];
                    index++;
                }
            }

            for (int channel = 0; channel < channelsPerStream; channel++)
            {
                for (int stream = 0; stream < nStreams; stream++)
                {
                    ephysData[stream * channelsPerStream + channel, sample] = data[index];
                    index++;
                }
            }
            index += nStreams; //filler words

            for (int channel = 0; channel < adcChannels; channel++)
            {
                adcData[channel, sample] = data[index];
                index++;
            }

            ttlInData[sample] = data[index];
            index++;
            ttlOutData[sample] = data[index];

        }

        bool CheckUsbHeader(ushort[] usbBuffer, int index)
        {
            ulong x1, x2, x3, x4, x5, x6, x7, x8;
            ulong header;

            x1 = usbBuffer[index];
          //  x2 = usbBuffer[index + 1];
            x3 = usbBuffer[index + 1];
          //  x4 = usbBuffer[index + 3];
            x5 = usbBuffer[index + 2];
          //  x6 = usbBuffer[index + 5];
            x7 = usbBuffer[index + 3];
           // x8 = usbBuffer[index + 7];

            header =  (x7 << 48) + (x5 << 32) +  (x3 << 16) +  (x1 << 0);
            return (header == RHD2000_HEADER_MAGIC_NUMBER);
        }
    }

}
