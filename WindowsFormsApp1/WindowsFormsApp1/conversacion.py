#Intento con ChatGPT y API key --- Fallido tengo q pagar

# -------------------------------------------------
# Módulo de interpretación conversacional para dron
# Compatible con la API nueva de OpenAI (2024-2025)
# -------------------------------------------------

import json
import requests

# -------------------------------------------------
# CONFIGURACIÓN DEL MODELO
# -------------------------------------------------
#OPENAI_API_KEY = "xxxxxx"
OPENAI_URL = "https://api.openai.com/v1/responses"
MODEL = "gpt-4o-mini"   # o gpt-4.1-mini


# -------------------------------------------------
# FUNCIÓN PRINCIPAL
# -------------------------------------------------
def interpretar(frase_usuario: str) -> dict:
    """
    Envía la frase del usuario al modelo LLM y devuelve un diccionario con:
    - accion
    - requiere_confirmacion
    - mensaje_hablado
    - razon
    """

    prompt = f"""
Eres un módulo de control para un dron. Analiza la frase y conviértela en una acción.

Responde SOLO en JSON:

{{
  "accion": "despegar | aterrizar | subir | bajar | girar_izq | girar_der | parar | confirmar | ninguna",
  "requiere_confirmacion": true/false,
  "mensaje_hablado": "mensaje para decir por altavoz",
  "razon": "explicación corta"
}}

Reglas:
- Si la frase indica una acción peligrosa → requiere_confirmacion = true.
- Si la frase está confirmando ("sí", "vale", "ok") → accion = "confirmar".
- Si no queda claro → accion = "ninguna".
- No hables fuera del JSON.

Frase del usuario: "{frase_usuario}"
"""

    headers = {
        "Authorization": f"Bearer {OPENAI_API_KEY}",
        "Content-Type": "application/json",
    }

    # -------------------------------------------------
    # API NUEVA → Se usa "input", no "messages"
    # -------------------------------------------------
    body = {
        "model": MODEL,
        "input": prompt,
        "temperature": 0.1
    }

    try:
        response = requests.post(OPENAI_URL, headers=headers, json=body)
        data = response.json()

        # -------------------------------------------------
        # API NUEVA → La salida viene en "output_text"
        # -------------------------------------------------
        raw_text = data["output_text"]

        resultado = json.loads(raw_text)
        return resultado

    except Exception as e:
        print("Error interpretando intención:", e)
        print("Respuesta recibida:", data)
        return {
            "accion": "ninguna",
            "requiere_confirmacion": False,
            "mensaje_hablado": "No he entendido lo que quieres hacer.",
            "razon": "error en el módulo"
        }


# -------------------------------------------------
# TEST RÁPIDO
# -------------------------------------------------
if __name__ == "__main__":
    while True:
        frase = input("\nTú: ")
        if frase.lower() in ("salir", "exit", "quit"):
            break

        resultado = interpretar(frase)
        print("\nIA:", json.dumps(resultado, indent=2, ensure_ascii=False))
