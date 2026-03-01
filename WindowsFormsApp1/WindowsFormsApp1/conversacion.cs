// conversacion.cs
// Reglas OFFLINE: rápidas, seguras y sin depender de IA.
// (No hace falta un conversacion_offline.cs adicional: este fichero ya cumple ese papel.)

using System;

namespace WindowsFormsApp1
{
    public static class Conversacion
    {
        public static (string accion, bool requiereConfirmacion, string mensaje) Interpretar(string frase)
        {
            frase = frase.ToLower().Trim();

            // Confirmaciones
            if (frase == "s" || frase == "sí" || frase == "si" || frase == "vale" || frase == "ok" || frase == "adelante")
                return ("confirmar", false, "Perfecto, ejecutando la orden.");

            // Conectar
            if (frase.Contains("conectar") || frase.Contains("conéctate") || frase.Contains("conectate"))
                return ("conectar", false, "Conectando al simulador.");

            // Despegar
            if (frase.Contains("despeg"))
                return ("despegar", true, "¿Confirmas que quieres que despegue?");

            // Aterrizar
            if (frase.Contains("aterriz"))
                return ("aterrizar", true, "¿Confirmas que quieres que aterrice?");

            // Subir
            if (frase.Contains("sube"))
                return ("subir", true, "¿Confirmas que quieres que suba?");

            // Bajar
            if (frase.Contains("baja"))
                return ("bajar", true, "¿Confirmas que quieres que baje?");

            // Girar izq
            if (frase.Contains("izquierda"))
                return ("girar_izq", false, "Girando a la izquierda.");

            // Girar der
            if (frase.Contains("derecha"))
                return ("girar_der", false, "Girando a la derecha.");

            // Avanzar
            if (frase.Contains("avanza") || frase.Contains("adelante"))
                return ("avanzar", false, "Avanzando.");

            // Nada detectado
            return ("ninguna", false, "No he entendido la orden.");
        }
    }
}