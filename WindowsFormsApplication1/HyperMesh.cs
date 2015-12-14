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
        int[] colorArray = new int[N];
        int[] labelArray = new int[N];
        public const int d = 15;
        public const int N = 32768;
        public int colors = 1;
        Random random = new Random();

        public int getDiameter()
        {
            return d;
        }
        public void labelMesh()
        {
            bool returnValue = true;
            //calculate component labeling using BFS for CPU
            Stopwatch cpuTime = Stopwatch.StartNew();
            int[] cpuLabelArray = this.runBFS(graphArray,colorArray, (int[])labelArray.Clone());
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

            
            //color array copy for GPU
            int[] copyToGpuColorArray = (int[])colorArray.Clone();
            int[] copyToGpuLabelArray = (int[])labelArray.Clone();
            //color array copy on GPU and transfer in next statement
            int[] deviceColorArray = gpu.Allocate<int>(copyToGpuColorArray);
            gpu.CopyToDevice(copyToGpuColorArray, deviceColorArray);

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
                    int[] deviceLabelArray = gpu.Allocate<int>(copyToGpuLabelArray);
                    gpu.CopyToDevice(copyToGpuLabelArray, deviceLabelArray);

                    gpuTime.Start();
                    gpu.Launch(N, 1, "kernelLabel", deviceColorArray, deviceLabelArray, deviceSwapOccured);
                    gpuTime.Stop();

                    gpu.CopyFromDevice(deviceLabelArray, copyToGpuLabelArray);
                    gpu.Free(deviceLabelArray);

                    gpu.CopyFromDevice(deviceSwapOccured, swapOccured);
                    for (int t = 0; t < N; t++)
                    {
                        runIterator = false;
                        if (swapOccured[t] == 1)
                        {
                            runIterator = true;
                            break;
                        }
                    }
                    iterations++;
                }

            }
            catch (Exception ex)
            {
                throw;
            }
            finally
            {
                gpu.Free(deviceSwapOccured);
                gpu.Free(deviceColorArray);
                for (int i = 0; i < N; i++)
                {
                    if (copyToGpuColorArray[i] != cpuLabelArray[i])
                    {
                        returnValue = false;
                        break;
                    }
                }
                copyToGpuColorArray = null;
                cpuLabelArray = null;
            }

            if (returnValue)
            {
             //uncomment this if data store is used
                //da.InsertResult(d, N, cpuTime.Elapsed.Ticks, gpuTime.Elapsed.Ticks, colors, 0, iterations);
            }
            else
             //uncomment this if data store is used.
                //da.InsertResult(d, N, 0, 0, colors, 0, iterations);

            km = null;
        }


        [Cudafy]
        public static void kernelLabel(GThread thread, int[] colorArray, int[] labelArray, int[] swapOccured)
        {

            int t_id = thread.blockIdx.x;
            t_id = t_id * 2;
            if (t_id < N)
            {
                swapOccured[t_id] = 0;
                for (int j = 0; j < d; j++)
                {
                    if (colorArray[t_id] == colorArray[(t_id ^ (1 << j))])
                    {
                        if (labelArray[t_id] > labelArray[(t_id ^ (1 << j))])
                        {
                            labelArray[t_id] = labelArray[(t_id ^ (1 << j))];
                            swapOccured[t_id] = 1;
                        }
                    }

                }
                t_id = t_id + 1;

            }
        }

        private int[] runBFS(int[,] graphArr, int[] colorArr,int[] labelArr)
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
                    if (colorArr[index] == colorArr[graphArr[index, i]] && labelArr[index] != labelArr[graphArr[index, i]])
                    {
                        if (labelArr[index] > labelArr[graphArr[index, i]])
                        {
                            labelArr[index] = labelArr[graphArr[index, i]];
                            queue.Enqueue(index);
                        }
                        else
                        {
                            labelArr[graphArr[index, i]] = labelArr[index];
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
            colorArray = new int[N];
            labelArray = new int[N];
            int size = (int)Math.Pow(2, d);
            int number = 0;
            for (int i = 0; i < size; i++)
            {
                labelArray[i] = i;
                colorArray[i] = random.Next(0, colors);

                number = i;
                for (int j = 0; j < d; j++)
                {
                    graphArray[i, j] = number ^ (1 << j);
                }
            }

        }


    }


}
