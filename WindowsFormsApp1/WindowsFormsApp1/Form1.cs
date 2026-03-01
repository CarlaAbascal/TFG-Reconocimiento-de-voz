using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Speech.Recognition;
using System.Speech.Synthesis;   
using System.Windows.Forms;
using csDronLink;
using WindowsFormsApp1;

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

        private bool hablando = false;

        private void Hablar(string texto)
        {
            // limpia cola para que no se acumulen frases
            voz.SpeakAsyncCancelAll();

            hablando = true;

            // parar reconocimiento mientras habla
            try { recognizer.RecognizeAsyncCancel(); } catch { }

            voz.SpeakCompleted -= Voz_SpeakCompleted;
            voz.SpeakCompleted += Voz_SpeakCompleted;

            voz.SpeakAsync(texto);
        }

        private void Voz_SpeakCompleted(object sender, System.Speech.Synthesis.SpeakCompletedEventArgs e)
        {
            hablando = false;

            // reanudar reconocimiento
            try { recognizer.RecognizeAsync(System.Speech.Recognition.RecognizeMode.Multiple); } catch { }
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
                "creo que ya podemos despegar",
                "conectate",
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
        // ==========================
        // Anti-repetición (debounce)
        // ==========================
        private string _ultimaFrase = "";
        private DateTime _ultimaFraseTs = DateTime.MinValue;

        private bool EsRepetida(string frase, int ms = 1200)
        {
            frase = (frase ?? "").Trim().ToLowerInvariant();

            if (frase == _ultimaFrase &&
                (DateTime.Now - _ultimaFraseTs).TotalMilliseconds < ms)
                return true;

            _ultimaFrase = frase;
            _ultimaFraseTs = DateTime.Now;
            return false;
        }

        // Evitar la repeticion ------ Campos de clase
        private string ultimoComando = "";
        private DateTime ultimoComandoTs = DateTime.MinValue;

        private bool EsComandoRepetido(string cmd, int ms = 1500)
        {
            if (cmd == ultimoComando && (DateTime.Now - ultimoComandoTs).TotalMilliseconds < ms)
                return true;

            ultimoComando = cmd;
            ultimoComandoTs = DateTime.Now;
            return false;
        }

        // >>> NUEVO — Intérprete conversacional offline
        // NOTA:
        // La interpretación OFFLINE está centralizada en Conversacion.cs
        // para evitar duplicar reglas dentro del Form.

        private void Recognizer_SpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            string frase = e.Result.Text;
            float conf = (float)e.Result.Confidence;
            if (hablando) return;

            // Anti-repetición: ignora repeticiones rápidas de la misma frase
            if (EsRepetida(frase, 1200)) return;

            this.BeginInvoke((MethodInvoker)delegate
            {
                this.Text = $"Reconocido: {frase} (conf {conf:F2})";

                // -------- LÓGICA ORIGINAL (NO TOCAR) --------
                // >>> si estamos esperando confirmación, SOLO procesamos confirmación
                if (esperandoConfirmacion)
                {
                    // Confirmación/cancelación se interpreta con reglas OFFLINE
                    var r = Conversacion.Interpretar(frase);

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

                // ------------------- OFFLINE primero -------------------
                // 1) Intentamos interpretar con reglas locales (seguro y rápido).
                //    Solo si NO se entiende, llamamos a IA (Ollama vía Python).
                var r0 = Conversacion.Interpretar(frase);

                if (r0.accion != "ninguna")
                {
                    Hablar(r0.mensaje);

                    if (r0.requiereConfirmacion)
                    {
                        pendienteAccion = r0.accion;
                        esperandoConfirmacion = true;
                        return;
                    }

                    EjecutarAccion(r0.accion);
                    return;
                }

                // ------------------- IA como fallback -------------------
                // IA SOLO AQUÍ (una sola vez, cuando offline no entiende)
                string respuestaIA = LlamarIA(frase);

                // >>> PARSER DE IA (IA devuelve intención estructurada)
                // Formato recomendado:
                //   ACTION=<accion>;CONFIRM=<0|1>;MSG=<texto>
                // Ejemplos:
                //   ACTION=despegar;CONFIRM=1;MSG=¿Confirmas que quieres que despegue?
                //   ACTION=girar_der;CONFIRM=0;MSG=Girando a la derecha.
                if (respuestaIA.StartsWith("ACTION="))
                {
                    string accionIA = "none";
                    bool confirmIA = false;
                    string msgIA = "";

                    // Parse robusto por tokens separados por ';'
                    var parts = respuestaIA.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var part in parts)
                    {
                        var kv = part.Split(new[] { '=' }, 2);
                        if (kv.Length != 2) continue;

                        string key = kv[0].Trim().ToUpperInvariant();
                        string val = kv[1].Trim();

                        if (key == "ACTION") accionIA = val;
                        else if (key == "CONFIRM") confirmIA = (val == "1" || val.Equals("true", StringComparison.OrdinalIgnoreCase));
                        else if (key == "MSG") msgIA = val;
                    }

                    // Mensaje (si existe)
                    if (!string.IsNullOrWhiteSpace(msgIA))
                        Hablar(msgIA);

                    // Si requiere confirmación
                    if (confirmIA)
                    {
                        pendienteAccion = accionIA;
                        esperandoConfirmacion = true;
                        return;
                    }

                    // Acción directa
                    if (accionIA == "none" || accionIA == "ninguna")
                    {
                        if (string.IsNullOrWhiteSpace(msgIA))
                            Hablar("No he entendido la orden.");
                        return;
                    }

                    EjecutarAccion(accionIA);
                    return;
                }

                // Si Python devuelve texto normal (por si lo usas en pruebas)
                Hablar(respuestaIA);
            });
        }

        // >>> NUEVO — Mapea acciones a botones reales
        private void EjecutarAccion(string accion)
        {
            switch (accion)
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

                case "girar_der":
                    button4.PerformClick();
                    break;

                case "girar_izq":
                    button5.PerformClick();
                    break;

                case "avanzar":
                    button6.PerformClick();
                    break;

                // >>> EXTRA — subir / bajar (si tu dron lo soporta)
                case "subir":
                    // Subir 2 m, modo no bloqueante
                    miDron.Mover("Up", 2, bloquear: false);
                    break;

                case "bajar":
                    // Bajar 2 m, modo no bloqueante
                    miDron.Mover("Down", 2, bloquear: false);
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
            try
            {
                miDron.Conectar("simulacion");
                miDron.EnviarDatosTelemetria(ProcesarTelemetria);

                Hablar("Conectado");
            }
            catch (Exception ex)
            {
                Hablar("No he podido conectar.");
                MessageBox.Show(ex.Message, "Error al conectar");
            }
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

                // >>> EXTRA — solo última línea útil
                var lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                return lines.Length > 0 ? lines[lines.Length - 1].Trim() : "IA ejecutada pero sin salida";

            }
            catch (Exception ex)
            {
                return "ERROR C#: " + ex.Message;
            }
        }
    }
}