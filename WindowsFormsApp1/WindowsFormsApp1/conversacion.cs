using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace WindowsFormsApp1
{
    public class ResultadoConversacion
    {
        public string Accion { get; set; }
        public bool RequiereConfirmacion { get; set; }
        public bool ComandoCompleto { get; set; }
        public string Mensaje { get; set; }
        public int? Grados { get; set; }
        public string Sentido { get; set; }
        public int? Metros { get; set; }

        public ResultadoConversacion()
        {
            Accion = "ninguna";
            RequiereConfirmacion = false;
            ComandoCompleto = true;
            Mensaje = "";
            Grados = null;
            Sentido = "";
            Metros = null;
        }
    }

    public static class Conversacion
    {
        private const int MIN_GRADOS = 1;
        private const int MAX_GRADOS = 360;
        private const int MIN_METROS = 1;
        private const int MAX_METROS = 100;

        // Se añaden valores algo superiores a la gramática para que, si el usuario dice
        // "gira 500 grados" o "avanza 150 metros", el sistema pueda reconocer la frase
        // y responder con el mensaje de rango permitido.
        private const int MAX_GRADOS_GRAMATICA = 500;
        private const int MAX_METROS_GRAMATICA = 200;

        private static OrdenPendiente ordenPendiente = null;

        private class OrdenPendiente
        {
            public string Accion { get; set; }
            public int? Grados { get; set; }
            public string Sentido { get; set; }
            public int? Metros { get; set; }

            public OrdenPendiente(string accion)
            {
                Accion = accion;
                Grados = null;
                Sentido = "";
                Metros = null;
            }
        }

        public static ResultadoConversacion Interpretar(string fraseOriginal)
        {
            string frase = Normalizar(fraseOriginal);

            if (string.IsNullOrWhiteSpace(frase))
                return Ninguna();

            if (EsConfirmacionSinContexto(frase))
            {
                return new ResultadoConversacion
                {
                    Accion = "confirmar",
                    Mensaje = "Perfecto, ejecutando la orden."
                };
            }

            if (EsRespuestaCancelacion(frase))
            {
                ordenPendiente = null;

                return new ResultadoConversacion
                {
                    Accion = "cancelar",
                    Mensaje = "Orden cancelada."
                };
            }

            if (ordenPendiente != null)
                return CompletarOrdenPendiente(frase);

            if (EsOrdenConectar(frase))
                return OrdenCritica("conectar", "¿Confirmas que quieres conectar el dron?");

            if (EsOrdenDespegar(frase))
                return OrdenCritica("despegar", "¿Confirmas que quieres que despegue?");

            if (EsOrdenAterrizar(frase))
                return OrdenCritica("aterrizar", "¿Confirmas que quieres que aterrice?");

            if (EsOrdenGiro(frase))
                return InterpretarGiro(frase);

            if (EsOrdenAvance(frase))
                return InterpretarAvance(frase);

            return Ninguna();
        }

        // ---------------------------------------------------------
        // ÓRDENES CRÍTICAS
        // ---------------------------------------------------------

        private static ResultadoConversacion OrdenCritica(string accion, string mensaje)
        {
            return new ResultadoConversacion
            {
                Accion = accion,
                RequiereConfirmacion = true,
                ComandoCompleto = true,
                Mensaje = mensaje
            };
        }

        private static bool EsOrdenConectar(string frase)
        {
            return frase.Contains("conect") || frase.Contains("conexion");
        }

        private static bool EsOrdenDespegar(string frase)
        {
            return frase.Contains("despeg");
        }

        private static bool EsOrdenAterrizar(string frase)
        {
            return frase.Contains("aterriz") || frase.Contains("aterric");
        }

        // ---------------------------------------------------------
        // GIRO
        // ---------------------------------------------------------

        private static ResultadoConversacion InterpretarGiro(string frase)
        {
            int? grados = ExtraerNumero(frase);
            string sentido = ExtraerSentido(frase);

            if (grados != null && !GradosValidos(grados.Value))
            {
                ordenPendiente = new OrdenPendiente("girar");
                ordenPendiente.Sentido = sentido;

                return ResultadoIncompleto(
                    "girar",
                    MensajeGradosInvalidos(sentido),
                    null,
                    sentido,
                    null
                );
            }

            if (grados == null || string.IsNullOrEmpty(sentido))
            {
                ordenPendiente = new OrdenPendiente("girar");
                ordenPendiente.Grados = grados;
                ordenPendiente.Sentido = sentido;

                return ResultadoIncompleto(
                    "girar",
                    MensajeFaltaInformacionGiro(grados, sentido),
                    grados,
                    sentido,
                    null
                );
            }

            return ResultadoGiroCompleto(grados.Value, sentido);
        }

        private static ResultadoConversacion CompletarGiroPendiente(string frase)
        {
            int? grados = ExtraerNumero(frase);
            string sentido = ExtraerSentido(frase);

            if (ordenPendiente.Grados == null && grados != null)
                ordenPendiente.Grados = grados;

            if (string.IsNullOrEmpty(ordenPendiente.Sentido) && !string.IsNullOrEmpty(sentido))
                ordenPendiente.Sentido = sentido;

            if (ordenPendiente.Grados != null && !GradosValidos(ordenPendiente.Grados.Value))
            {
                ordenPendiente.Grados = null;

                return ResultadoIncompleto(
                    "girar",
                    MensajeGradosInvalidos(ordenPendiente.Sentido),
                    null,
                    ordenPendiente.Sentido,
                    null
                );
            }

            if (ordenPendiente.Grados != null && !string.IsNullOrEmpty(ordenPendiente.Sentido))
            {
                int gradosFinales = ordenPendiente.Grados.Value;
                string sentidoFinal = ordenPendiente.Sentido;
                ordenPendiente = null;

                return ResultadoGiroCompleto(gradosFinales, sentidoFinal);
            }

            return ResultadoIncompleto(
                "girar",
                MensajeFaltaInformacionGiro(ordenPendiente.Grados, ordenPendiente.Sentido),
                ordenPendiente.Grados,
                ordenPendiente.Sentido,
                null
            );
        }

        private static ResultadoConversacion ResultadoGiroCompleto(int grados, string sentido)
        {
            return new ResultadoConversacion
            {
                Accion = "girar",
                ComandoCompleto = true,
                Grados = grados,
                Sentido = sentido,
                Mensaje = "Girando " + grados + " grados a la " + sentido + "."
            };
        }

        private static string MensajeFaltaInformacionGiro(int? grados, string sentido)
        {
            bool faltanGrados = grados == null;
            bool faltaSentido = string.IsNullOrEmpty(sentido);

            if (faltanGrados && faltaSentido)
                return "¿Cuántos grados y hacia qué lado quieres girar? Los grados deben estar entre 1 y 360.";

            if (faltanGrados)
                return "¿Cuántos grados quieres girar a la " + sentido + "? Los grados deben estar entre 1 y 360.";

            return "¿Hacia qué lado quieres girar " + grados.Value + " grados, derecha o izquierda?";
        }

        private static string MensajeGradosInvalidos(string sentido)
        {
            if (!string.IsNullOrEmpty(sentido))
                return "Los grados deben estar entre 1 y 360. ¿Cuántos grados quieres girar a la " + sentido + "?";

            return "Los grados deben estar entre 1 y 360. ¿Cuántos grados quieres girar?";
        }

        private static bool EsOrdenGiro(string frase)
        {
            if (frase.Contains("gir") || frase.Contains("rot"))
                return true;

            if (!string.IsNullOrEmpty(ExtraerSentido(frase)))
                return true;

            int? numero = ExtraerNumero(frase);
            return numero != null && frase.Contains("grado");
        }

        private static string ExtraerSentido(string frase)
        {
            if (frase.Contains("derecha") || frase.Contains("derecho") || frase.Contains("dreta"))
                return "derecha";

            if (frase.Contains("izquierda") || frase.Contains("izquierdo") || frase.Contains("esquerra"))
                return "izquierda";

            return "";
        }

        private static bool GradosValidos(int grados)
        {
            return grados >= MIN_GRADOS && grados <= MAX_GRADOS;
        }

        // ---------------------------------------------------------
        // AVANCE
        // ---------------------------------------------------------

        private static ResultadoConversacion InterpretarAvance(string frase)
        {
            int? metros = ExtraerNumero(frase);

            if (metros != null && !MetrosValidos(metros.Value))
            {
                ordenPendiente = new OrdenPendiente("avanzar");

                return ResultadoIncompleto(
                    "avanzar",
                    MensajeMetrosInvalidos(),
                    null,
                    "",
                    null
                );
            }

            if (metros == null)
            {
                ordenPendiente = new OrdenPendiente("avanzar");

                return ResultadoIncompleto(
                    "avanzar",
                    MensajeFaltanMetros(),
                    null,
                    "",
                    null
                );
            }

            return ResultadoAvanceCompleto(metros.Value);
        }

        private static ResultadoConversacion CompletarAvancePendiente(string frase)
        {
            int? metros = ExtraerNumero(frase);

            if (metros == null)
            {
                return ResultadoIncompleto(
                    "avanzar",
                    MensajeFaltanMetros(),
                    null,
                    "",
                    null
                );
            }

            if (!MetrosValidos(metros.Value))
            {
                return ResultadoIncompleto(
                    "avanzar",
                    MensajeMetrosInvalidos(),
                    null,
                    "",
                    null
                );
            }

            ordenPendiente = null;
            return ResultadoAvanceCompleto(metros.Value);
        }

        private static ResultadoConversacion ResultadoAvanceCompleto(int metros)
        {
            return new ResultadoConversacion
            {
                Accion = "avanzar",
                ComandoCompleto = true,
                Metros = metros,
                Mensaje = "Avanzando " + metros + " metros."
            };
        }

        private static string MensajeFaltanMetros()
        {
            return "¿Cuántos metros quieres avanzar? La distancia debe estar entre 1 y 100 metros.";
        }

        private static string MensajeMetrosInvalidos()
        {
            return "La distancia debe estar entre 1 y 100 metros. ¿Cuántos metros quieres avanzar?";
        }

        private static bool EsOrdenAvance(string frase)
        {
            if (frase.Contains("avanz") || frase.Contains("adelante") || frase.Contains("delante"))
                return true;

            if (frase.Contains("mueve") && (frase.Contains("adelante") || frase.Contains("delante")))
                return true;

            if (frase.Contains("muevete") && (frase.Contains("adelante") || frase.Contains("delante")))
                return true;

            return false;
        }

        private static bool MetrosValidos(int metros)
        {
            return metros >= MIN_METROS && metros <= MAX_METROS;
        }

        // ---------------------------------------------------------
        // ORDEN PENDIENTE
        // ---------------------------------------------------------

        private static ResultadoConversacion CompletarOrdenPendiente(string frase)
        {
            if (ordenPendiente.Accion == "girar")
                return CompletarGiroPendiente(frase);

            if (ordenPendiente.Accion == "avanzar")
                return CompletarAvancePendiente(frase);

            ordenPendiente = null;
            return Ninguna();
        }

        private static ResultadoConversacion ResultadoIncompleto(string accion, string mensaje, int? grados, string sentido, int? metros)
        {
            return new ResultadoConversacion
            {
                Accion = accion,
                ComandoCompleto = false,
                Mensaje = mensaje,
                Grados = grados,
                Sentido = sentido,
                Metros = metros
            };
        }

        // ---------------------------------------------------------
        // CONFIRMACIÓN / CANCELACIÓN
        // ---------------------------------------------------------

        public static bool EsRespuestaConfirmacion(string frase)
        {
            frase = Normalizar(frase);

            return frase == "si" || frase == "vale" || frase == "ok" ||
                   frase == "confirmar" || frase == "confirmo" || frase == "confirma" ||
                   frase == "adelante" || frase == "correcto" || frase == "de acuerdo" ||
                   frase == "hazlo" || frase == "ejecuta";
        }

        public static bool EsRespuestaCancelacion(string frase)
        {
            frase = Normalizar(frase);

            return frase == "no" || frase == "cancelar" || frase == "cancela" ||
                   frase == "cancelado" || frase == "para" || frase == "stop";
        }

        private static bool EsConfirmacionSinContexto(string frase)
        {
            // "adelante" no se trata como confirmación fuera de una confirmación pendiente,
            // porque también puede significar avanzar hacia delante.
            return frase == "si" || frase == "vale" || frase == "ok" ||
                   frase == "confirmar" || frase == "confirmo" || frase == "confirma" ||
                   frase == "correcto" || frase == "de acuerdo" || frase == "hazlo" ||
                   frase == "ejecuta";
        }

        // ---------------------------------------------------------
        // NÚMEROS
        // ---------------------------------------------------------

        private static int? ExtraerNumero(string frase)
        {
            Match match = Regex.Match(frase, @"\b\d{1,4}\b");

            if (match.Success)
                return int.Parse(match.Value);

            return ExtraerNumeroEnPalabras(frase);
        }

        private static int? ExtraerNumeroEnPalabras(string frase)
        {
            List<string> palabras = new List<string>();

            foreach (Match m in Regex.Matches(frase, @"[a-zA-ZñÑ]+"))
                palabras.Add(Normalizar(m.Value));

            int? mejorValor = null;
            int mejorLongitud = 0;

            for (int i = 0; i < palabras.Count; i++)
            {
                for (int longitud = 1; longitud <= 6 && i + longitud <= palabras.Count; longitud++)
                {
                    List<string> trozo = palabras.GetRange(i, longitud);
                    int? valor = ParsearNumeroPalabras(trozo);

                    if (valor != null && longitud > mejorLongitud)
                    {
                        mejorValor = valor;
                        mejorLongitud = longitud;
                    }
                }
            }

            return mejorValor;
        }

        private static int? ParsearNumeroPalabras(List<string> palabrasOriginales)
        {
            List<string> palabras = new List<string>();

            foreach (string p in palabrasOriginales)
            {
                if (p != "y")
                    palabras.Add(p);
            }

            if (palabras.Count == 0)
                return null;

            int total = 0;
            int indice = 0;

            if (EsCentena(palabras[0]))
            {
                total += ValorCentena(palabras[0]);
                indice = 1;
            }

            if (indice >= palabras.Count)
                return total;

            List<string> resto = palabras.GetRange(indice, palabras.Count - indice);
            int? valorResto = ParsearMenorDeCien(resto);

            if (valorResto == null)
                return null;

            return total + valorResto.Value;
        }

        private static int? ParsearMenorDeCien(List<string> palabras)
        {
            if (palabras.Count == 0)
                return 0;

            string texto = string.Join(" ", palabras.ToArray());

            Dictionary<string, int> especiales = new Dictionary<string, int>
            {
                { "cero", 0 },
                { "un", 1 }, { "uno", 1 }, { "una", 1 },
                { "dos", 2 }, { "tres", 3 }, { "cuatro", 4 }, { "cinco", 5 },
                { "seis", 6 }, { "siete", 7 }, { "ocho", 8 }, { "nueve", 9 },
                { "diez", 10 }, { "once", 11 }, { "doce", 12 }, { "trece", 13 },
                { "catorce", 14 }, { "quince", 15 }, { "dieciseis", 16 },
                { "diecisiete", 17 }, { "dieciocho", 18 }, { "diecinueve", 19 },
                { "veinte", 20 }, { "veintiuno", 21 }, { "veintidos", 22 },
                { "veintitres", 23 }, { "veinticuatro", 24 }, { "veinticinco", 25 },
                { "veintiseis", 26 }, { "veintisiete", 27 }, { "veintiocho", 28 },
                { "veintinueve", 29 }
            };

            if (especiales.ContainsKey(texto))
                return especiales[texto];

            Dictionary<string, int> decenas = new Dictionary<string, int>
            {
                { "treinta", 30 }, { "cuarenta", 40 }, { "cincuenta", 50 },
                { "sesenta", 60 }, { "setenta", 70 }, { "ochenta", 80 }, { "noventa", 90 }
            };

            if (palabras.Count == 1 && decenas.ContainsKey(palabras[0]))
                return decenas[palabras[0]];

            if (palabras.Count == 2 && decenas.ContainsKey(palabras[0]) && especiales.ContainsKey(palabras[1]))
                return decenas[palabras[0]] + especiales[palabras[1]];

            return null;
        }

        private static bool EsCentena(string palabra)
        {
            return palabra == "cien" || palabra == "ciento" ||
                   palabra == "doscientos" || palabra == "trescientos" ||
                   palabra == "cuatrocientos" || palabra == "quinientos";
        }

        private static int ValorCentena(string palabra)
        {
            if (palabra == "doscientos") return 200;
            if (palabra == "trescientos") return 300;
            if (palabra == "cuatrocientos") return 400;
            if (palabra == "quinientos") return 500;
            return 100;
        }

        private static string NumeroAPalabras(int numero)
        {
            if (numero < 0 || numero > 500)
                return numero.ToString();

            string[] unidades =
            {
                "cero", "uno", "dos", "tres", "cuatro", "cinco", "seis", "siete", "ocho", "nueve",
                "diez", "once", "doce", "trece", "catorce", "quince", "dieciseis", "diecisiete",
                "dieciocho", "diecinueve", "veinte", "veintiuno", "veintidos", "veintitres",
                "veinticuatro", "veinticinco", "veintiseis", "veintisiete", "veintiocho", "veintinueve"
            };

            if (numero < 30)
                return unidades[numero];

            string[] decenas = { "", "", "", "treinta", "cuarenta", "cincuenta", "sesenta", "setenta", "ochenta", "noventa" };

            if (numero < 100)
            {
                int d = numero / 10;
                int u = numero % 10;

                if (u == 0)
                    return decenas[d];

                return decenas[d] + " y " + unidades[u];
            }

            if (numero == 100) return "cien";
            if (numero < 200) return "ciento " + NumeroAPalabras(numero - 100);
            if (numero == 200) return "doscientos";
            if (numero < 300) return "doscientos " + NumeroAPalabras(numero - 200);
            if (numero == 300) return "trescientos";
            if (numero < 400) return "trescientos " + NumeroAPalabras(numero - 300);
            if (numero == 400) return "cuatrocientos";
            if (numero < 500) return "cuatrocientos " + NumeroAPalabras(numero - 400);

            return "quinientos";
        }

        // ---------------------------------------------------------
        // NORMALIZACIÓN Y GRAMÁTICA
        // ---------------------------------------------------------

        private static ResultadoConversacion Ninguna()
        {
            return new ResultadoConversacion
            {
                Accion = "ninguna",
                Mensaje = "No he entendido la orden."
            };
        }

        private static string Normalizar(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
                return "";

            texto = texto.ToLower().Trim();
            string normalized = texto.Normalize(NormalizationForm.FormD);
            StringBuilder sb = new StringBuilder();

            foreach (char c in normalized)
            {
                UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(c);
                if (category != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }

            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        public static string[] ObtenerFrasesReconocibles()
        {
            List<string> frases = new List<string>();

            AgregarFrasesBase(frases);
            AgregarFrasesConNumeros(frases);

            return EliminarDuplicados(frases);
        }

        private static void AgregarFrasesBase(List<string> frases)
        {
            frases.AddRange(new string[]
            {
                // Conectar
                "conectar", "conecta", "conectate", "conéctate", "conectarte",
                "puedes conectar", "puedes conectarte", "quiero conectar", "quiero que conectes",
                "conecta el dron", "conectar el dron", "iniciar conexion", "iniciar conexión",

                // Despegar
                "despegar", "despega", "despegue", "despegues",
                "puedes despegar", "quiero despegar", "quiero que despegues", "quiero que despegue",
                "despega el dron", "hacer despegar", "haz que despegue",

                // Aterrizar
                "aterrizar", "aterriza", "aterrice", "aterrices",
                "puedes aterrizar", "quiero aterrizar", "quiero que aterrices", "quiero que aterrice",
                "aterriza el dron", "hacer aterrizar", "haz que aterrice",

                // Confirmar / cancelar
                "si", "sí", "vale", "ok", "confirmar", "confirmo", "confirma", "adelante", "correcto", "de acuerdo", "hazlo", "ejecuta",
                "no", "cancelar", "cancela", "cancelado", "para", "stop",

                // Avanzar sin metros
                "avanzar", "avanza", "adelante", "hacia delante", "avanza el dron",
                "puedes avanzar", "quiero que avances", "quiero avanzar",
                "mueve adelante", "muevete adelante", "muévete adelante",
                "mueve hacia delante", "muevete hacia delante", "muévete hacia delante",

                // Girar sin parámetros completos
                "gira", "girar", "rota", "rotar",
                "derecha", "izquierda",
                "gira a la derecha", "gira a la izquierda",
                "girar a la derecha", "girar a la izquierda",
                "puedes girar a la derecha", "puedes girar a la izquierda",
                "quiero que gires a la derecha", "quiero que gires a la izquierda"
            });
        }

        private static void AgregarFrasesConNumeros(List<string> frases)
        {
            for (int n = 1; n <= MAX_GRADOS_GRAMATICA; n++)
            {
                string numeroTexto = NumeroAPalabras(n);
                string numeroDigitos = n.ToString();

                AgregarFrasesGiro(frases, numeroTexto, "derecha");
                AgregarFrasesGiro(frases, numeroTexto, "izquierda");
                AgregarFrasesGiro(frases, numeroDigitos, "derecha");
                AgregarFrasesGiro(frases, numeroDigitos, "izquierda");

                AgregarFrasesGiroSinSentido(frases, numeroTexto);
                AgregarFrasesGiroSinSentido(frases, numeroDigitos);

                frases.Add(numeroTexto);
                frases.Add(numeroTexto + " grados");
                frases.Add(numeroDigitos);
                frases.Add(numeroDigitos + " grados");

                if (n <= MAX_METROS_GRAMATICA)
                {
                    AgregarFrasesAvance(frases, numeroTexto);
                    AgregarFrasesAvance(frases, numeroDigitos);
                    frases.Add(numeroTexto + " metros");
                    frases.Add(numeroDigitos + " metros");
                }
            }
        }

        private static void AgregarFrasesGiroSinSentido(List<string> frases, string numero)
        {
            frases.Add("gira " + numero + " grados");
            frases.Add("girar " + numero + " grados");
            frases.Add("rota " + numero + " grados");
            frases.Add("rotar " + numero + " grados");
            frases.Add("gira " + numero);
            frases.Add("girar " + numero);
            frases.Add("quiero que gires " + numero + " grados");
            frases.Add("puedes girar " + numero + " grados");
        }

        private static void AgregarFrasesGiro(List<string> frases, string numero, string sentido)
        {
            frases.Add("gira a la " + sentido + " " + numero + " grados");
            frases.Add("gira " + numero + " grados a la " + sentido);
            frases.Add("gira " + numero + " a la " + sentido);
            frases.Add("gira " + sentido + " " + numero + " grados");
            frases.Add("girar a la " + sentido + " " + numero + " grados");
            frases.Add("girar " + numero + " grados a la " + sentido);
            frases.Add("rota a la " + sentido + " " + numero + " grados");
            frases.Add("rota " + numero + " grados a la " + sentido);
            frases.Add(sentido + " " + numero + " grados");
            frases.Add(numero + " grados " + sentido);
            frases.Add("puedes girar a la " + sentido + " " + numero + " grados");
            frases.Add("puedes girar " + numero + " grados a la " + sentido);
            frases.Add("quiero que gires a la " + sentido + " " + numero + " grados");
            frases.Add("quiero que gires " + numero + " grados a la " + sentido);
        }

        private static void AgregarFrasesAvance(List<string> frases, string numero)
        {
            frases.Add("avanza " + numero + " metros");
            frases.Add("avanzar " + numero + " metros");
            frases.Add("avanza " + numero);
            frases.Add("avanzar " + numero);
            frases.Add("adelante " + numero + " metros");
            frases.Add("hacia delante " + numero + " metros");
            frases.Add("mueve " + numero + " metros hacia delante");
            frases.Add("muevete " + numero + " metros hacia delante");
            frases.Add("muévete " + numero + " metros hacia delante");
            frases.Add("quiero avanzar " + numero + " metros");
            frases.Add("quiero que avances " + numero + " metros");
            frases.Add("puedes avanzar " + numero + " metros");
        }

        private static string[] EliminarDuplicados(List<string> frases)
        {
            HashSet<string> sinDuplicados = new HashSet<string>();
            List<string> resultado = new List<string>();

            foreach (string frase in frases)
            {
                string limpia = frase.Trim();

                if (!string.IsNullOrWhiteSpace(limpia) && !sinDuplicados.Contains(limpia))
                {
                    sinDuplicados.Add(limpia);
                    resultado.Add(limpia);
                }
            }

            return resultado.ToArray();
        }
    }
}
