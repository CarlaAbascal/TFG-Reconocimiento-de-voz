using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using csDronLink;
using System.Speech.Recognition; // Reconocimiento de voz

namespace WindowsFormsApp1
{
    public partial class Form1 : Form
    {
        Dron miDron = new Dron();
        SpeechRecognitionEngine recognizer; // Reconocimiento de voz

        public Form1()
        {
            InitializeComponent();
            CheckForIllegalCrossThreadCalls = false; // Para evitar problemas de hilos con los labels
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // INICIALIZAMOS RECONOCIMIENTO DE VOZ
            recognizer = new SpeechRecognitionEngine();

            // Definimos comandos de voz
            Choices commands = new Choices();
            commands.Add(new string[] {
                "conectar",
                "despegar",
                "aterrizar",
                "derecha",
                "izquierda",
                "avanzar",
                "detener"
            });

            GrammarBuilder gb = new GrammarBuilder();
            gb.Append(commands);

            Grammar g = new Grammar(gb);
            recognizer.LoadGrammar(g);

            // Cuando reconoce algo
            recognizer.SpeechRecognized += Recognizer_SpeechRecognized;

            recognizer.SetInputToDefaultAudioDevice();
            recognizer.RecognizeAsync(RecognizeMode.Multiple);
        }

        private void Recognizer_SpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            string comando = e.Result.Text;

            this.BeginInvoke((MethodInvoker)delegate //Mete la acción en cola de la UI y no bloquea el reconocimiento
            {
                this.Text = $"Reconocido: {comando}";

                switch (comando)
                {
                    //Simula el click a los botones 
                    case "conectar":
                        button1.PerformClick();
                        break;
                    case "despegar":
                        button2.PerformClick();
                        break;
                    case "aterrizar":
                        button3.PerformClick();
                        break;
                    case "derecha":
                        button4.PerformClick();
                        break;
                    case "izquierda":
                        button5.PerformClick();
                        break;
                    case "avanzar":
                        button6.PerformClick();
                        break;
                    case "detener":
                        button7.PerformClick();
                        break;
                }
            });
        }


        // Recorrer la lista con los datos de telemetría y mostrar la altitud en un label
        private void ProcesarTelemetria(byte id, List<(string nombre, float valor)> telemetria)
        {
            foreach (var t in telemetria)
            {
                if (t.nombre == "Alt")
                {
                    altLbl.Text = t.valor.ToString();
                    break;
                }
            }
        }

        private void button1_Click_1(object sender, EventArgs e)
        {
            miDron.Conectar("simulacion");
            miDron.EnviarDatosTelemetria(ProcesarTelemetria);
        }

        private void EnAire(byte id, object param)
        {
            button2.BackColor = Color.Green;
            button2.ForeColor = Color.White;
            button2.Text = (string)param;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            miDron.Despegar(20, bloquear: false, f: EnAire, param: "Volando");
            button2.BackColor = Color.Yellow;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            miDron.Aterrizar();
        }

        private void button4_Click(object sender, EventArgs e)
        {
            // Girar a la derecha 90º
            miDron.CambiarHeading(90);
        }

        private void button5_Click(object sender, EventArgs e)
        {
            // Girar a la izquierda -90º
            miDron.CambiarHeading(-90);
        }

        private void button6_Click(object sender, EventArgs e)
        {
            // Avanzar 10 m
            miDron.Mover("Forward", 10);
        }

        private void button7_Click(object sender, EventArgs e)
        {
            // Detener movimiento
            miDron.Mover("Stop", 0);
        }
    }
}
