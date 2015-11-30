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
        DataAdapter da = new DataAdapter();

        int[,] graphArray = new int[N, d + 1];
        /// <summary>
        /// (i,0) is label
        /// (i,1) is color or weight
        /// </summary>
        int[,] colorArray = new int[N, 2];
        public const int d = 11;
        public const int N = 4096;
        public int colors = 1;
        Random random = new Random();

        public int getDiameter()
        {
            return d;
        }
        public void labelMesh()
        {
            bool returnValue = true;
            string returnString = "";
            //calculate component labeling using BFS for CPU
            Stopwatch cpuTime = Stopwatch.StartNew();
            int[,] cpuColorArray = this.runBFS(graphArray, (int[,])colorArray.Clone());
            cpuTime.Stop();

            CudafyModule km = CudafyModule.TryDeserialize();
            CudafyTranslator.GenerateDebug = true;
            if (km == null || !km.TryVerifyChecksums())
            {
                km = CudafyTranslator.Cudafy();
                km.TrySerialize();
            }

            GPGPU gpu = CudafyHost.GetDevice(CudafyModes.Target, CudafyModes.DeviceId);
            gpu.LoadModule(km);

            //graph array
            int[,] deviceGraphArray = gpu.Allocate<int>(graphArray);
            gpu.CopyToDevice(graphArray, deviceGraphArray);

            //color array copy for GPU
            int[,] copyToGpuColorArray = (int[,])colorArray.Clone();
            //color array copy on GPU and transfer in next statement

            //comparer
            int[] swapOccured = new int[N];
            for (int i = 0; i < N; i++)
                swapOccured[i] = 0;
            int[] deviceSwapOccured = gpu.Allocate(swapOccured);
            gpu.CopyToDevice(swapOccured, deviceSwapOccured);


            Stopwatch gpuTime = new Stopwatch();
            int iterations = 0;
            bool runIterator = true;
            try
            {

                while (runIterator)
                {
                    int[,] deviceColorArray = gpu.Allocate<int>(copyToGpuColorArray);
                    gpu.CopyToDevice(copyToGpuColorArray, deviceColorArray);

                    gpuTime.Start();
                    gpu.Launch(N, 1, "kernelLabel", deviceGraphArray, deviceColorArray, deviceSwapOccured);
                    gpuTime.Stop();

                    gpu.CopyFromDevice(deviceColorArray, copyToGpuColorArray);
                    gpu.Free(deviceColorArray);

                    
                    if(Helper.CompareArrayEqual(copyToGpuColorArray,cpuColorArray))
                         runIterator = false;

                    if(iterations>N)
                        runIterator = false;
                    //gpu.CopyFromDevice(deviceSwapOccured, swapOccured);
                    //for (int t = 0; t < N; t++)
                    //{
                    //    runIterator = false;
                    //    if (swapOccured[t] == 1)
                    //    {
                    //        runIterator = true;
                    //        break;
                    //    }
                    //}
                    iterations++;
                }

            }
            catch (Exception ex)
            {
                throw;
            }
            finally
            {
                gpu.Free(deviceGraphArray);
                gpu.Free(deviceSwapOccured);
                for (int i = 0; i < N; i++)
                {
                    if (copyToGpuColorArray[i, 0] != cpuColorArray[i, 0])
                    {
                        returnValue = false;
                        break;
                    }
                }
                copyToGpuColorArray = null;
                cpuColorArray = null;
            }

            if (returnValue)
            {
                da.InsertResult(d, N, cpuTime.Elapsed.Ticks, gpuTime.Elapsed.Ticks, colors, 0, iterations);
            }
            else
                da.InsertResult(d, N, 0, 0, colors, 0, iterations);

            km = null;
        }


        [Cudafy]
        public static void kernelLabel(GThread thread, int[,] graphArray, int[,] colorArray, int[] swapOccured)
        {

            //int t_id = thread.blockIdx.x;
            int t_id = thread.blockIdx.x*thread.blockDim.x+thread.threadIdx.x;
            if (t_id < N)
            {

                swapOccured[t_id] = 0;
                int number = t_id;
                for (int j = 0; j < d; j++)
                {
                    if (colorArray[t_id, 1] == colorArray[number ^ (1 << j), 1])
                    {
                        if (colorArray[t_id, 0] > colorArray[number ^ (1 << j), 0])
                        {
                            colorArray[t_id, 0] = colorArray[number ^ (1 << j), 0];
                            swapOccured[t_id] = 1;
                        }
                    }

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
                for (int i = 0; i < d; i++)
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
            hashset.Clear();
            hashset = null;
            queue = null;
            return colorArr;
        }

        /// <summary>
        /// Method generates hyper cube graph. 
        /// It also assigns a random property value to each vertex.
        /// </summary>
        public void GenerateGraph()
        {
            graphArray = new int[N, d];
            colorArray = new int[N, 2];
            int size = (int)Math.Pow(2, d);
            int number = 0;
            for (int i = 0; i < size; i++)
            {
                //graphArray[i, 0] = i;
                colorArray[i, 0] = i;

                colorArray[i, 1] = random.Next(0, colors);

                number = i;
                for (int j = 0; j < d; j++)
                {
                    graphArray[i, j] = number ^ (1 << j);
                }
            }

        }


    }


}
