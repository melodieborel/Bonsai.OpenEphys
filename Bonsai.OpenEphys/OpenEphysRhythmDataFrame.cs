using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenCV.Net;

namespace Bonsai.OpenEphys
{
    public class OpenEphysRhythmDataFrame
    {
        public OpenEphysRhythmDataFrame(RhythmData dataBlock, double bufferCapacity)
        {
            Timestamp = GetTimestampData(dataBlock.Timestamps);
            AmplifierData = GetStreamData(dataBlock.EphysData);
            AuxiliaryData = GetAuxiliaryData(dataBlock.AuxData);
            BoardAdcData = GetStreamData(dataBlock.AdcData);
            TtlIn = GetTtlData(dataBlock.TtlInData);
            TtlOut = GetTtlData(dataBlock.TtlOutData);
            BufferCapacity = bufferCapacity;
        }

        Mat GetTimestampData(uint[] data)
        {
            return Mat.FromArray(data, 1, data.Length, Depth.S32, 1);
        }

        Mat GetTtlData(ushort[] data)
        {
            var output = new Mat(1, data.Length, Depth.U16, 1);
            using (var header = Mat.CreateMatHeader(data))
            {
                CV.Convert(header, output);
            }

            return output;
        }

        Mat GetStreamData(ushort[,] data)
        {
            var numChannels = data.GetLength(0);
            var numSamples = data.GetLength(1);

            var output = new Mat(numChannels, numSamples, Depth.U16, 1);
            using (var header = Mat.CreateMatHeader(data))
            {
                CV.Convert(header, output);
            }

            return output;
        }

        Mat GetAuxiliaryData(ushort[][,] data)
        {
            const int AuxDataChannels = 4;
            const int OutputChannels = AuxDataChannels - 1;
            if (data.Length == 0) return null;
            var numSamples = data[0].GetLength(1) / AuxDataChannels;
            var auxData = new ushort[OutputChannels, numSamples];
            for (int i = 0; i < numSamples; i++)
            {
                for (int j=0; j < OutputChannels; j++)
                {
                    auxData[j, i] = data[0][1, (j+1)%AuxDataChannels + i * AuxDataChannels];
                }
            }

            var output = new Mat(OutputChannels, numSamples, Depth.U16, 1);
            using (var header = Mat.CreateMatHeader(auxData))
            {
                CV.Convert(header, output);
            }

            return output;
        }


        public Mat Timestamp { get; private set; }

        public Mat AmplifierData { get; private set; }

        public Mat AuxiliaryData { get; private set; }

        public Mat BoardAdcData { get; private set; }

        public Mat TtlIn { get; private set; }

        public Mat TtlOut { get; private set; }

        public double BufferCapacity { get; private set; }
    }
}
