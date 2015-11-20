using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LabelComponent
{
    class Helper
    {
        /// <summary>
        /// This method is custom method to handle two dimension array with each element containing two children.
        /// </summary>
        /// <param name="array1"></param>
        /// <param name="array2"></param>
        /// <returns></returns>
        public static bool EqualArrayContent(int[,] array1, int[,] array2, int rows)
        {
            if(array1.Length!=array2.Length)
                return false;
            for (int i = 0; i < rows; i++)
            {
                if (array1[i,0] != array2[i,0])
                {
                    return false;
                }
            }
            return true;
        }
    }
}
