using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cudafy.Host;
using Cudafy.Translator;
using Cudafy.Types;
using Cudafy;
using System.Diagnostics;
/*
 Class to generate hypermesh class labeling on parallel systems.
 
 
 */
namespace LabelComponent
{
    class HyperMesh
    {

        int[,] graphArray = new int[N, d + 1];
        /// <summary>
        /// (i,0) is label
        /// (i,1) is color or weight
        /// </summary>
        int[,] colorArray = new int[N, 2];
        public const int d = 12;
        public const int N = 4096;
        Random random = new Random();

        public string labelMesh()
        {
            bool returnValue = true;
            string returnString = "";
            //calculate component labeling using BFS for CPU
            Stopwatch s = Stopwatch.StartNew();
            int[,] cpuColorArray = this.runBFS(graphArray, (int[,])colorArray.Clone());
            s.Stop();

            returnString = "CPU TIME : " + s.Elapsed.Ticks;

            int[,] gpuColorArray = (int[,])colorArray.Clone();
            CudafyModule km = CudafyModule.TryDeserialize();
            if (km == null || !km.TryVerifyChecksums())
            {
                km = CudafyTranslator.Cudafy();
                km.TrySerialize();
            }
            GPGPU gpu = CudafyHost.GetDevice(CudafyModes.Target, CudafyModes.DeviceId);
            gpu.LoadModule(km);


            int[,] deviceGraphArray = gpu.Allocate<int>(graphArray);
            gpu.CopyToDevice(graphArray, deviceGraphArray);
            try
            {
                bool repeat = true;
                s = Stopwatch.StartNew();
                s.Stop();
                while (repeat)
                {
                    int[,] deviceColorArray = gpu.Allocate<int>(gpuColorArray);
                    gpu.CopyToDevice(gpuColorArray, deviceColorArray);
                    s.Start();
                    gpu.Launch(N, 1, "kernelLabel", deviceGraphArray, deviceColorArray);
                    s.Stop();

                    int[,] compareArray = (int[,])gpuColorArray.Clone();
                    gpu.CopyFromDevice(deviceColorArray, gpuColorArray);
                    gpu.Free(deviceColorArray);
                    if (Helper.EqualArrayContent(compareArray, gpuColorArray, N))
                        repeat = false;

                }
            }
            catch (Exception ex)
            {

            }
            finally
            {
                gpu.Free(deviceGraphArray);

                returnString += " : GPU TIME : " + s.Elapsed.Ticks;
                for (int i = 0; i < (gpuColorArray.Length) / 2; i++)
                {
                    if (gpuColorArray[i, 0] != cpuColorArray[i, 0])
                    {
                        returnValue = false;
                        break;
                    }
                }
            }

            if (returnValue)
                return returnString;
            else
                return "no match";
        }


        [Cudafy]
        public static void kernelLabel(GThread thread, int[,] graphArray, int[,] colorArray)
        {
            //isChanged = 0;
            int t_id = thread.blockIdx.x;
            if (t_id < N)
            {
                int device_d = d;
                while (device_d > 0)
                {
                    if (colorArray[t_id, 1] == colorArray[graphArray[t_id, device_d], 1])
                    {
                        if (colorArray[t_id, 0] == colorArray[graphArray[t_id, device_d], 0])
                        {
                            continue;
                        }
                        if (colorArray[t_id, 0] > colorArray[graphArray[t_id, device_d], 0])
                        {
                            colorArray[t_id, 0] = colorArray[graphArray[t_id, device_d], 0];
                        }
                        else
                            colorArray[graphArray[t_id, device_d], 0] = colorArray[t_id, 0];
                    }
                    device_d--;
                }
            }
        }

        private int[,] runBFS(int[,] graphArr, int[,] colorArr)
        {
            Queue<int> queue = new Queue<int>();
            HashSet<int> hashset = new HashSet<int>();

            int index = 0;
            hashset.Add(index);
            queue.Enqueue(index);
            while (queue.Count > 0)
            {
                index = queue.Dequeue();
                for (int i = 1; i <= d; i++)
                {
                    if (!hashset.Contains(graphArr[index, i]))
                    {
                        queue.Enqueue(graphArr[index, i]);
                        hashset.Add(graphArr[index, i]);
                    }
                    if (colorArr[index, 1] == colorArr[graphArr[index, i], 1] && colorArr[index, 0] != colorArr[graphArr[index, i], 0])
                    {
                        if (colorArr[index, 0] > colorArr[graphArr[index, i], 0])
                        {
                            colorArr[index, 0] = colorArr[graphArr[index, i], 0];
                            queue.Enqueue(index);
                        }
                        else
                        {
                            colorArr[graphArr[index, i], 0] = colorArr[index, 0];
                            queue.Enqueue(index);
                        }
                    }


                }
            }
            return colorArr;
        }

        /// <summary>
        /// Method generates hyper cube graph. 
        /// It also assigns a random property value to each vertex.
        /// </summary>
        public void GenerateGraph()
        {
            int size = (int)Math.Pow(2, d);
            int number = 0;
            for (int i = 0; i < size; i++)
            {
                graphArray[i, 0] = i;
                colorArray[i, 0] = i;

                colorArray[i, 1] = (int)(random.NextDouble() * (size / 3));

                number = i;
                for (int j = 0; j < d; j++)
                {
                    graphArray[i, j + 1] = number ^ (1 << j);
                }
            }

        }


    }


}
