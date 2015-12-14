# LabelComponent
This program does component labeling for connected elements that share a common property. We have written program that runs on CPU and GPU. For CPU, I am using BFS, and for GPU, I am using nearest Neighbor propagation method.

Kernel A – Neighbor Propagation
  This algorithm is a multi-pass labelling method. It parallelizes the task of labelling by creating one thread for each cell which loads the field and label data from its cell and the neighboring cells. The threads will then find the cell with the lowest label and the same state as its allocated cell. If the label is less than the current label, the cell will be updated with the new lower label.
  For each iteration, each cell will get the lowest label of the neighboring cells with the same property. The kernel must be called multiple times until no label changes occur, then it is known that every cell has been assigned its correct final label. To determine if any label has changed during a kernel call, a global Boolean is used. If a kernel changes a label it will set this Boolean to true. The host program can then examine this Boolean value to determine if another iteration of the algorithm is required.
  Algorithm for Kernel program is given below. This algorithm is described in [4]. Host program is running on CPU and Kernel program is running on GPU.

GPU program is based on :
Parallel Graph Component Labelling with GPUs and CUDA

K.A. Hawick, A. Leist and D.P. Playne
http://www.massey.ac.nz/~dpplayne/Papers/cstn-089.pdf
