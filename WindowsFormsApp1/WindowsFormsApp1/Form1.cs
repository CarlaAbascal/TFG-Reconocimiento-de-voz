using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Speech.Recognition;
using System.Windows.Forms;
using csDronLink;

namespace WindowsFormsApp1
{
    public partial class Form1 : Form
    {
        Dron miDron = new Dron();
        SpeechRecognitionEngine recognizer; // Reconocimiento de voz

        public Form1()
        {
            InitializeComponent();
            CheckForIllegalCrossThreadCalls = false;
            this.FormClosing += Form1_FormClosing;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // Intentar usar recognizer en español (fallback a constructor por defecto si falla)
            try
            {
                recognizer = new SpeechRecognitionEngine(new CultureInfo("es-ES"));
            }
            catch
            {
                recognizer = new SpeechRecognitionEngine();
            }

            // Definimos comandos de voz (añade variantes si sueles decir frases)
            Choices commands = new Choices();
            commands.Add(new string[] {
                "conectar",
                "despegar",
                "aterrizar",
                "derecha",
                "izquierda",
                "girar a la derecha",
                "girar a la izquierda",
                "avanzar"
            });

            GrammarBuilder gb = new GrammarBuilder();
            gb.Append(commands);

            Grammar g = new Grammar(gb);
            recognizer.LoadGrammar(g);

            // Eventos principales
            recognizer.SpeechRecognized += Recognizer_SpeechRecognized;

            // Reintentos automáticos si se completa o se rechaza
            recognizer.RecognizeCompleted += (s, ev) =>
            {
                // si hubo error; volvemos a escuchar siempre que no haya sido disposed
                try
                {
                    if (recognizer != null)
                        recognizer.RecognizeAsync(RecognizeMode.Multiple);
                }
                catch { }
            };

            recognizer.SpeechRecognitionRejected += (s, ev) =>
            {
                try
                {
                    if (recognizer != null)
                        recognizer.RecognizeAsync(RecognizeMode.Multiple);
                }
                catch { }
            };

            recognizer.SetInputToDefaultAudioDevice();
            recognizer.RecognizeAsync(RecognizeMode.Multiple);
        }

        private void Recognizer_SpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            string comando = e.Result.Text;
            float conf = (float)e.Result.Confidence;

          
            // Ejecutar en cola de UI (no bloquea el hilo del recognizer)
            this.BeginInvoke((MethodInvoker)delegate
            {
                // feedback
                this.Text = $"Reconocido: {comando} (conf {conf:F2})";

                switch (comando)
                {
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
                    case "girar a la derecha":
                        button4.PerformClick();
                        break;
                    case "izquierda":
                    case "girar a la izquierda":
                        button5.PerformClick();
                        break;
                    case "avanzar":
                        button6.PerformClick();
                        break;
                }
            });
        }

        // Telemetría (sin cambios)
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
            // ya usabas bloquear:false, correcto para que no bloquee la UI
            miDron.Despegar(20, bloquear: false, f: EnAire, param: "Volando");
            button2.BackColor = Color.Yellow;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            // usar no bloqueante
            miDron.Aterrizar(bloquear: false);
        }

        private void button4_Click(object sender, EventArgs e)
        {
            // Girar a la derecha a 90º, no bloqueante
            miDron.CambiarHeading(90, bloquear: false);
        }

        private void button5_Click(object sender, EventArgs e)
        {
            // Girar a la izquierda: el -90 NO funciona -- por lo tanto 270
            miDron.CambiarHeading(270, bloquear: false);
        }

        private void button6_Click(object sender, EventArgs e)
        {
            // Avanzar 10 m, modo no bloqueante
            miDron.Mover("Forward", 10, bloquear: false);
        }

        
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                if (recognizer != null)
                {
                    recognizer.RecognizeAsyncCancel();
                    recognizer.RecognizeAsyncStop();
                    recognizer.Dispose();
                    recognizer = null;
                }
            }
            catch { }
        }
    }
}
