using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Speech.Recognition;
using System.Speech.Synthesis;   // >>> NUEVO
using System.Windows.Forms;
using csDronLink;

namespace WindowsFormsApp1
{
    public partial class Form1 : Form
    {
        Dron miDron = new Dron();
        SpeechRecognitionEngine recognizer; // Reconocimiento de voz
        SpeechSynthesizer voz = new SpeechSynthesizer();   // >>> NUEVO

        // >>> NUEVO — estado conversacional
        string pendienteAccion = "";
        bool esperandoConfirmacion = false;

        public Form1()
        {
            InitializeComponent();
            CheckForIllegalCrossThreadCalls = false;
            this.FormClosing += Form1_FormClosing;

            voz.Rate = 0;   // velocidad normal
        }

        private void Hablar(string texto)   // >>> NUEVO
        {
            voz.SpeakAsync(texto);
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
                "avanzar",

                // >>> NUEVO — algunas frases naturales típicas
                "creo que ya podemos despegar",
                "sube",
                "baja",
                "sí",
                "si",
                "vale",
                "ok",
                "adelante"
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

        // >>> NUEVO — Intérprete conversacional offline

        private (string accion, bool requiereConfirmacion, string mensaje) Interpretar(string frase)
        {
            frase = frase.ToLower().Trim();

            // Confirmaciones
            if (frase == "si" || frase == "sí" || frase == "vale" || frase == "ok" || frase == "adelante")
                return ("confirmar", false, "Perfecto, ejecutando la orden.");

            // >>> NUEVO: Conectar
            if (frase.Contains("conectar") || frase.Contains("conéctate") || frase.Contains("conectate"))
                return ("conectar", false, "Conectando al simulador.");

            // Despegar
            if (frase.Contains("despeg"))
                return ("despegar", true, "¿Confirmas que quieres que despegue?");

            // Aterrizar
            if (frase.Contains("aterriz"))
                return ("aterrizar", true, "¿Confirmas que quieres que aterrice?");

            // >>> ELIMINADO: subir / bajar

            // Girar izquierda
            if (frase.Contains("izquierda"))
                return ("girar_izq", false, "Girando a la izquierda.");

            // Girar derecha
            if (frase.Contains("derecha"))
                return ("girar_der", false, "Girando a la derecha.");

            // Avanzar
            if (frase.Contains("avanza"))
                return ("avanzar", false, "Avanzando.");

            return ("ninguna", false, "No he entendido la orden.");
        }

        private void Recognizer_SpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            string frase = e.Result.Text;
            float conf = (float)e.Result.Confidence;

            this.BeginInvoke((MethodInvoker)delegate
            {
                this.Text = $"Reconocido: {frase} (conf {conf:F2})";

                //IA SIEMPRE AQUÍ (una sola vez)
                string respuestaIA = LlamarIA(frase);
                Hablar(respuestaIA);
                return;

                // -------- LÓGICA ORIGINAL (NO TOCAR) --------

                if (esperandoConfirmacion)
                {
                    var r = Interpretar(frase);

                    if (r.accion == "confirmar")
                    {
                        Hablar("Confirmado.");
                        EjecutarAccion(pendienteAccion);
                    }
                    else
                    {
                        Hablar("Cancelado.");
                    }

                    esperandoConfirmacion = false;
                    pendienteAccion = "";
                    return;
                }

                var resultado = Interpretar(frase);
                string accion = resultado.accion;
                bool requiere = resultado.requiereConfirmacion;
                string mensaje = resultado.mensaje;

                if (accion == "ninguna")
                {
                    Hablar(mensaje);
                    return;
                }

                if (requiere)
                {
                    Hablar(mensaje);
                    pendienteAccion = accion;
                    esperandoConfirmacion = true;
                    return;
                }

                EjecutarAccion(accion);
            });
        }

        // >>> NUEVO — Mapea acciones a botones reales
        private void EjecutarAccion(string accion)
        {
            switch (accion)
            {
                case "conectar":        // >>> NUEVO
                    button1.PerformClick();
                    break;

                case "despegar":
                    button2.PerformClick();
                    break;

                case "aterrizar":
                    button3.PerformClick();
                    break;

                case "girar_der":
                    button4.PerformClick();
                    break;

                case "girar_izq":
                    button5.PerformClick();
                    break;

                case "avanzar":
                    button6.PerformClick();
                    break;

                default:
                    Hablar("Acción no reconocida.");
                    break;
            }
        }


        // Telemetría
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

        // ----------------------BOTONES-------------------------
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


        private string LlamarIA(string frase)
        {
            try
            {
                var p = new System.Diagnostics.Process();

                // Python de Anaconda (correcto)
                p.StartInfo.FileName = @"C:\Users\CARLA\miniconda3\python.exe";

                // Ruta CORRECTA al script (verbatim string)
                string script = @"C:\Users\CARLA\Desktop\UNIVERSITAT\TFG\TFG-Reconocimiento-de-voz\WindowsFormsApp1\WindowsFormsApp1\ia_simple.py";

                p.StartInfo.Arguments = $"\"{script}\" \"{frase}\"";
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                p.StartInfo.CreateNoWindow = true;

                p.Start();

                string output = p.StandardOutput.ReadToEnd();
                string error = p.StandardError.ReadToEnd();

                p.WaitForExit();

                if (!string.IsNullOrWhiteSpace(error))
                {
                    MessageBox.Show(error, "ERROR PYTHON");
                    return "Error en la IA";
                }


                if (string.IsNullOrWhiteSpace(output))
                    return "IA ejecutada pero sin salida";

                return output.Trim();
            }
            catch (Exception ex)
            {
                return "ERROR C#: " + ex.Message;
            }
        }




    }
}
