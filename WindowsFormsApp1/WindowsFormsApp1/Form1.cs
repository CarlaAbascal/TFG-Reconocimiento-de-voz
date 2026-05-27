using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Speech.Recognition;
using System.Speech.Synthesis;
using System.Windows.Forms;
using csDronLink;

namespace WindowsFormsApp1
{
    public partial class Form1 : Form
    {
        private Dron miDron = new Dron();

        private SpeechRecognitionEngine recognizer;
        private SpeechSynthesizer voz = new SpeechSynthesizer();

        // Estado de confirmación: solo se usa para conectar, despegar y aterrizar.
        private bool esperandoConfirmacion = false;
        private ResultadoConversacion ordenPendienteConfirmacion = null;

        // Filtro para evitar dobles detecciones muy seguidas.
        private string ultimaFrase = "";
        private DateTime ultimaFraseTiempo = DateTime.MinValue;

        // La gramática cerrada es más fiable porque solo contiene órdenes del dron.
        // El dictado libre necesita más confianza porque puede reconocer ruido o frases extrañas.
        private const float CONFIANZA_MINIMA_COMANDO = 0.30f;
        private const float CONFIANZA_MINIMA_DICTADO = 0.55f;

        public Form1()
        {
            InitializeComponent();

            CheckForIllegalCrossThreadCalls = false;
            this.FormClosing += Form1_FormClosing;

            voz.Rate = 0;
            voz.SetOutputToDefaultAudioDevice();
        }

        private void Hablar(string texto)
        {
            try
            {
                voz.SpeakAsyncCancelAll();
                voz.SpeakAsync(texto);
            }
            catch
            {
                // Evita que un fallo de audio cierre la aplicación.
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            InicializarReconocimientoVoz();
        }

        private void InicializarReconocimientoVoz()
        {
            try
            {
                recognizer = new SpeechRecognitionEngine(new CultureInfo("es-ES"));
            }
            catch
            {
                recognizer = new SpeechRecognitionEngine();
            }

            Choices commands = new Choices();
            commands.Add(Conversacion.ObtenerFrasesReconocibles());

            GrammarBuilder gb = new GrammarBuilder();
            gb.Culture = recognizer.RecognizerInfo.Culture;
            gb.Append(commands);

            Grammar grammar = new Grammar(gb);
            grammar.Name = "ComandosControlados";
            recognizer.LoadGrammar(grammar);

            // Apoyo para frases que no estén exactamente en la lista.
            // Si da problemas, se puede eliminar y dejar solo ComandosControlados.
            try
            {
                DictationGrammar dictado = new DictationGrammar();
                dictado.Name = "DictadoLibre";
                recognizer.LoadGrammar(dictado);
            }
            catch
            {
                // No todos los equipos tienen dictado disponible para es-ES.
            }

            recognizer.SpeechRecognized += Recognizer_SpeechRecognized;
            recognizer.SpeechRecognitionRejected += Recognizer_SpeechRecognitionRejected;

            recognizer.SetInputToDefaultAudioDevice();
            recognizer.RecognizeAsync(RecognizeMode.Multiple);

            Hablar("Reconocimiento de voz activado.");
        }

        private void Recognizer_SpeechRecognitionRejected(object sender, SpeechRecognitionRejectedEventArgs e)
        {
            this.BeginInvoke((MethodInvoker)delegate
            {
                this.Text = "No se ha reconocido la orden";
            });
        }

        private void Recognizer_SpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            string frase = e.Result.Text;
            float confianza = (float)e.Result.Confidence;
            string nombreGramatica = e.Result.Grammar != null ? e.Result.Grammar.Name : "";

            this.BeginInvoke((MethodInvoker)delegate
            {
                float confianzaMinima = nombreGramatica == "ComandosControlados"
                    ? CONFIANZA_MINIMA_COMANDO
                    : CONFIANZA_MINIMA_DICTADO;

                this.Text = "Reconocido: " + frase + " (" + confianza.ToString("F2") + ") - " + nombreGramatica;

                if (confianza < confianzaMinima)
                {
                    this.Text = this.Text + " - ignorado por baja confianza";
                    return;
                }

                // Evita que la misma frase se ejecute dos veces si el motor la detecta repetida.
                if (frase == ultimaFrase &&
                    (DateTime.Now - ultimaFraseTiempo).TotalMilliseconds < 1200)
                {
                    return;
                }

                ultimaFrase = frase;
                ultimaFraseTiempo = DateTime.Now;

                ProcesarOrdenVoz(frase);
            });
        }

        private void ProcesarOrdenVoz(string frase)
        {
            if (esperandoConfirmacion)
            {
                if (Conversacion.EsRespuestaConfirmacion(frase))
                {
                    Hablar("Confirmado.");
                    EjecutarAccion(ordenPendienteConfirmacion);
                    LimpiarConfirmacionPendiente();
                    return;
                }

                if (Conversacion.EsRespuestaCancelacion(frase))
                {
                    Hablar("Cancelado.");
                    LimpiarConfirmacionPendiente();
                    return;
                }

                Hablar("Responde sí para confirmar o no para cancelar.");
                return;
            }

            ResultadoConversacion resultado = Conversacion.Interpretar(frase);

            if (resultado.Accion == "confirmar" || resultado.Accion == "cancelar")
            {
                Hablar("No hay ninguna orden pendiente.");
                return;
            }

            if (resultado.Accion == "ninguna")
            {
                Hablar(resultado.Mensaje);
                return;
            }

            if (!resultado.ComandoCompleto)
            {
                Hablar(resultado.Mensaje);
                return;
            }

            if (resultado.RequiereConfirmacion)
            {
                esperandoConfirmacion = true;
                ordenPendienteConfirmacion = resultado;
                Hablar(resultado.Mensaje);
                return;
            }

            Hablar(resultado.Mensaje);
            EjecutarAccion(resultado);
        }

        private void LimpiarConfirmacionPendiente()
        {
            esperandoConfirmacion = false;
            ordenPendienteConfirmacion = null;
        }

        private void EjecutarAccion(ResultadoConversacion resultado)
        {
            if (resultado == null)
            {
                Hablar("No hay ninguna acción pendiente.");
                return;
            }

            switch (resultado.Accion)
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

                case "girar":
                    EjecutarGiro(resultado.Sentido, resultado.Grados);
                    break;

                case "avanzar":
                    EjecutarAvance(resultado.Metros);
                    break;

                default:
                    Hablar("Acción no reconocida.");
                    break;
            }
        }

        private void EjecutarGiro(string sentido, int? grados)
        {
            if (grados == null || string.IsNullOrEmpty(sentido))
            {
                Hablar("No se puede girar porque faltan grados o sentido.");
                return;
            }

            try
            {
                int heading;

                if (sentido == "derecha")
                {
                    heading = grados.Value % 360;
                }
                else
                {
                    heading = (360 - grados.Value) % 360;
                }

                miDron.CambiarHeading(heading, bloquear: false);
            }
            catch (Exception ex)
            {
                this.Text = "Error al girar: " + ex.Message;
                Hablar("No he podido girar. Revisa que el dron esté conectado.");
            }
        }

        private bool EjecutarAvance(int? metros)
        {
            if (metros == null)
            {
                Hablar("No se puede avanzar porque faltan los metros.");
                return false;
            }

            try
            {
                miDron.Mover("Forward", metros.Value, bloquear: false);
                return true;
            }
            catch (Exception ex)
            {
                this.Text = "Error al avanzar: " + ex.Message;
                Hablar("No he podido avanzar. Revisa que el dron esté conectado.");
                return false;
            }
        }

        // ---------------------- TELEMETRÍA ----------------------

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

        // ---------------------- BOTONES -------------------------

        private void button1_Click_1(object sender, EventArgs e)
        {
            try
            {
                miDron.Conectar("simulacion");
                miDron.EnviarDatosTelemetria(ProcesarTelemetria);
                Hablar("Dron conectado.");
            }
            catch (Exception ex)
            {
                this.Text = "Error al conectar: " + ex.Message;
                Hablar("No he podido conectar con el dron.");
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
            try
            {
                miDron.Despegar(20, bloquear: false, f: EnAire, param: "Volando");
                button2.BackColor = Color.Yellow;
                Hablar("Despegando.");
            }
            catch (Exception ex)
            {
                this.Text = "Error al despegar: " + ex.Message;
                Hablar("No he podido despegar. Revisa que el dron esté conectado.");
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            try
            {
                miDron.Aterrizar(bloquear: false);
                Hablar("Aterrizando.");
            }
            catch (Exception ex)
            {
                this.Text = "Error al aterrizar: " + ex.Message;
                Hablar("No he podido aterrizar.");
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            try
            {
                miDron.CambiarHeading(90, bloquear: false);
            }
            catch (Exception ex)
            {
                this.Text = "Error al girar derecha: " + ex.Message;
            }
        }

        private void button5_Click(object sender, EventArgs e)
        {
            try
            {
                miDron.CambiarHeading(270, bloquear: false);
            }
            catch (Exception ex)
            {
                this.Text = "Error al girar izquierda: " + ex.Message;
            }
        }

        private void button6_Click(object sender, EventArgs e)
        {
            if (EjecutarAvance(10))
                Hablar("Avanzando 10 metros.");
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                if (recognizer != null)
                {
                    recognizer.RecognizeAsyncCancel();
                    recognizer.Dispose();
                    recognizer = null;
                }

                if (voz != null)
                {
                    voz.SpeakAsyncCancelAll();
                    voz.Dispose();
                    voz = null;
                }
            }
            catch
            {
                // Evita errores al cerrar la aplicación.
            }
        }
    }
}
