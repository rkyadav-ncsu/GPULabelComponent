using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using Cudafy;
using Cudafy.Host;
using Cudafy.Types;
using Cudafy.Translator;

namespace LabelComponent
{
    public partial class Form1 : Form
    {
        bool loaded = false;
        public Form1()
        {
            InitializeComponent();

        }
        private void button1_Click(object sender, EventArgs e)
        {
            lblSecond.Text = "started";
            HyperMesh hyperMesh = new HyperMesh();
            int N = hyperMesh.getDiameter();

            
            for (int i = 1; i <= N; i++)
            {
                if (i == 6)
                    break;
                hyperMesh.colors = i;
                for (int j = 0; j < 11; j++)
                {
                    hyperMesh.GenerateGraph();
                    hyperMesh.labelMesh();
                }
            }

            lblSecond.Text = "finished";

        }

    }
}
