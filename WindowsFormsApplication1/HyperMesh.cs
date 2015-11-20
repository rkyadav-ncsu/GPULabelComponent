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
        public struct Node
        {
            public int Data;
            public int Color;
            public int Label;
            public int[] Neighbors;
        }
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
            Stopwatch s = Stopwatch.StartNew();
            int[,] cpuColorArray = this.runBFS(graphArray, (int[,])colorArray.Clone());
            s.Stop();
            returnString= "CPU TIME : "+ s.Elapsed.Ticks;
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
            //int[,] deviceColorArray = gpu.Allocate<int>(gpuColorArray);
            gpu.CopyToDevice(graphArray, deviceGraphArray);
            try
            {
                //gpu.Launch(grids, threads, ((Action<GThread, Sphere[], byte[]>)thekernel), s, dev_bitmap); // Strongly typed
                {
                    bool repeat=true;
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
            }
            catch (Exception ex)
            {

            }
            finally
            {
                //gpu.Free(deviceColorArray);
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
        public void CreatArray(Node[] collection)
        {
            for (int i = 0; i < collection.Length; i++)
            {
                colorArray[i, 0] = collection[i].Label;
                colorArray[i, 1] = collection[i].Color;
                graphArray[i, 0] = i;
                for (int inner = 0; inner < d; inner++)
                {
                    graphArray[i, inner + 1] = collection[i].Neighbors[inner];
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
        ///converts a list of dictionary into array. 
        ///input is expected to be 
        ///{(1, 1, 0): [(1, 0, 0), (0, 1, 0), (1, 1, 1)], 
        ///(0, 1, 1): [(0, 1, 0), (0, 0, 1), (1, 1, 1)], 
        ///(1, 0, 0): [(1, 1, 0), (0, 0, 0), (1, 0, 1)], 
        ///(0, 0, 1): [(0, 1, 1), (0, 0, 0), (1, 0, 1)], 
        ///(1, 0, 1): [(1, 0, 0), (1, 1, 1), (0, 0, 1)], 
        ///(0, 0, 0): [(1, 0, 0), (0, 1, 0), (0, 0, 1)], 
        ///(0, 1, 0): [(1, 1, 0), (0, 0, 0), (0, 1, 1)], 
        ///(1, 1, 1): [(0, 1, 1), (1, 1, 0), (1, 0, 1)]}
        /// </summary>
        public void Converter()
        {
            string text = System.IO.File.ReadAllText(@"C:\Users\ravi\Google Drive\Master of Science\Ex Algorithm\Project Output Files\output"+d+".txt");
            text = text.Replace('{', ' ').Replace('}', ' ').Trim();
            int size = d;
            int totalSize = (int)Math.Pow(2, size);
            for (int i = 0; i < totalSize; i++)
            {
                string firstNode = text.Substring(text.IndexOf("("), text.IndexOf(")"));
                firstNode = firstNode.Replace(",", "").Replace(" ", "").Replace(")", "").Replace("(", "").Replace(":", "").Trim();

                int currentNode = binaryToInt(firstNode);
                text = text.Substring(text.IndexOf(":") + 3);
                string tempString = text.Substring(0, text.IndexOf("]"));
                text = text.Substring(text.IndexOf("]") + 1);

                //update the array
                graphArray[currentNode, 0] = currentNode;
                colorArray[currentNode, 0] = currentNode;
                //random weight

                colorArray[currentNode, 1] = (int)(random.NextDouble() * (totalSize/3));

                for (int r = 0; r < size; r++)
                {
                    string childNode = tempString.Substring(tempString.IndexOf("(") + 1, tempString.IndexOf(")"));
                    tempString = tempString.Contains("), ") ? tempString.Substring(tempString.IndexOf(")") + 3) : "";
                    childNode = childNode.Replace(",", "").Replace(" ", "").Replace(")", "").Replace("(", "").Trim();
                    graphArray[currentNode, r + 1] = binaryToInt(childNode);
                }
            }
        }
        public  void GenerateGraph()
        {
            int size = (int)Math.Pow(2, d);
            int number = 0;
            for (int i = 0; i < size; i++)
            {
                graphArray[i, 0] = i;
                colorArray[i, 0] = i;
                number = i;
                for (int j = 0; j < d; j++)
                {
                    graphArray[i, j + 1] = number ^ (1 << j);
                    //Console.Out.WriteLine(number + " - " + (number ^ (1 << j)));
                }
            }

        }
        /// <summary>
        /// Converts binary string into Interger
        /// </summary>
        /// <param name="binaryString">String containing binary</param>
        /// <returns>Integer equivalent of input binary</returns>
        private int binaryToInt(string binaryString)
        {
            int returnValue = 0;
            for (int i = binaryString.Length - 1; i >= 0; i--)
            {
                returnValue = returnValue + ((int)Math.Pow(2, binaryString.Length - i - 1) * Convert.ToInt16(binaryString[i].ToString()));
            }
            return returnValue;
        }
        public Node[] createMeshNodeCollection()
        {
            Random random = new Random();
            Node[] Collection = new Node[8];
            //another 1
            Node node = new Node() { Data = 0, Label = 0, Color = 780 };
            node.Neighbors = new int[d];
            node.Neighbors[0] = (1);
            node.Neighbors[1] = (4);
            node.Neighbors[2] = (3);
            Collection[0] = node;
            //another 2
            node = new Node() { Data = 1, Label = 1, Color = 232 };
            node.Neighbors = new int[d];
            node.Neighbors[0] = (0);
            node.Neighbors[1] = (2);
            node.Neighbors[2] = (5);
            Collection[1] = node;
            //another 3
            node = new Node() { Data = 2, Label = 2, Color = 232 };
            node.Neighbors = new int[d];
            node.Neighbors[0] = (1);
            node.Neighbors[1] = (3);
            node.Neighbors[2] = (6);
            Collection[2] = node;
            //another 4
            node = new Node() { Data = 3, Label = 3, Color = 123 };
            node.Neighbors = new int[d];
            node.Neighbors[0] = (0);
            node.Neighbors[1] = (2);
            node.Neighbors[2] = (7);
            Collection[3] = node;
            //another 5
            node = new Node() { Data = 4, Label = 4, Color = 154 };
            node.Neighbors = new int[d];
            node.Neighbors[0] = (0);
            node.Neighbors[1] = (5);
            node.Neighbors[2] = (7);
            Collection[4] = node;
            //another 6
            node = new Node() { Data = 5, Label = 5, Color = 154 };
            node.Neighbors = new int[d];
            node.Neighbors[0] = (1);
            node.Neighbors[1] = (4);
            node.Neighbors[2] = (6);
            Collection[5] = node;
            //another 7
            node = new Node() { Data = 6, Label = 6, Color = 123 };
            node.Neighbors = new int[d];
            node.Neighbors[0] = (2);
            node.Neighbors[1] = (5);
            node.Neighbors[2] = (7);
            Collection[6] = node;
            //another 8
            node = new Node() { Data = 7, Label = 7, Color = 123 };
            node.Neighbors = new int[d];
            node.Neighbors[0] = (6);
            node.Neighbors[1] = (4);
            node.Neighbors[2] = (3);
            Collection[7] = node;




            return Collection;
        }

    }


}
