# ia_simple.py
# ------------------
# Interpretación de intención con Ollama (LLM local).
# Devuelve una salida ESTRUCTURADA para C#:
#   ACTION=<accion>;CONFIRM=<0|1>;MSG=<texto>
#
# Notas:
# - Ollama API local: http://localhost:11434/api/chat
# - No requiere librerías externas (solo stdlib).
# - Incluye un fallback de reglas por si Ollama no está levantado.

import os
import sys
import json
import re
import urllib.request
import urllib.error

# ---------------------------- CONFIG ---------------------------------
OLLAMA_URL = os.environ.get("OLLAMA_URL", "http://localhost:11434/api/chat")
OLLAMA_MODEL = os.environ.get("OLLAMA_MODEL", "llama3.2")
OLLAMA_TIMEOUT = float(os.environ.get("OLLAMA_TIMEOUT", "12"))  # segundos

# ---------------------------- INPUT ----------------------------------
# Frase original
frase = sys.argv[1].lower() if len(sys.argv) > 1 else ""

# Normalizar: quitar signos
frase_norm = re.sub(r"[^\w\s]", "", frase).strip()

# ---------------------------- OUTPUT ---------------------------------
# Helper para imprimir en el formato que C# espera

def out(action: str, confirm: int = 0, msg: str = ""):
    # MSG es opcional, pero ayuda a que C# diga algo más natural
    if msg:
        print(f"ACTION={action};CONFIRM={confirm};MSG={msg}")
    else:
        print(f"ACTION={action};CONFIRM={confirm}")

# ------------------------ FALLBACK OFFLINE ----------------------------
# Reglas simples por si Ollama no está disponible.
# (Esto evita que el sistema se quede "mudo".)

def fallback_rules(text: str):
    # Conectar
    if "conectar" in text or "conéctate" in text or "conectate" in text:
        return ("conectar", 0, "Conectando al simulador.")

    # Confirmaciones
    if text in ("s", "sí", "si", "vale", "ok", "adelante"):
        return ("confirmar", 0, "Perfecto, ejecutando la orden.")

    # Despegar
    if "despeg" in text:
        return ("despegar", 1, "¿Confirmas que quieres que despegue?")

    # Aterrizar
    if "aterriz" in text:
        return ("aterrizar", 1, "¿Confirmas que quieres que aterrice?")

    # Subir
    if "sube" in text:
        return ("subir", 1, "¿Confirmas que quieres que suba?")

    # Bajar
    if "baja" in text:
        return ("bajar", 1, "¿Confirmas que quieres que baje?")

    # Avanzar
    if "avanza" in text or "adelante" in text:
        return ("avanzar", 0, "Avanzando.")

    # Girar izquierda
    if "izquierda" in text:
        return ("girar_izq", 0, "Girando a la izquierda.")

    # Girar derecha
    if "derecha" in text:
        return ("girar_der", 0, "Girando a la derecha.")

    return ("none", 0, "No he entendido la orden.")

# -------------------------- OLLAMA CALL --------------------------------

def call_ollama(user_text: str) -> str:
    """Devuelve el contenido de texto del asistente (string)."""

    system_prompt = (
        "Eres un clasificador de intenciones para controlar un dron. "
        "Debes responder SIEMPRE en UNA SOLA LÍNEA y SOLO con el formato exacto:\n"
        "ACTION=<accion>;CONFIRM=<0|1>;MSG=<mensaje>\n\n"
        "Acciones válidas (usa exactamente estas etiquetas):\n"
        "- conectar\n- despegar\n- aterrizar\n- subir\n- bajar\n- girar_izq\n- girar_der\n- avanzar\n- confirmar\n- none\n\n"
        "Reglas:\n"
        "- Si la orden implica riesgo (despegar, aterrizar, subir, bajar) usa CONFIRM=1.\n"
        "- Para 'sí/si/vale/ok/adelante' usa ACTION=confirmar y CONFIRM=0.\n"
        "- Si no entiendes, ACTION=none y CONFIRM=0.\n"
        "- MSG debe estar en español, corto y natural.\n"
        "- No añadas comillas, ni JSON, ni texto extra.\n"
    )

    payload = {
        "model": OLLAMA_MODEL,
        "stream": False,
        "messages": [
            {"role": "system", "content": system_prompt},
            {"role": "user", "content": user_text},
        ],
        # keep_alive: mantiene el modelo en memoria un rato para ir más rápido
        "keep_alive": "10m",
        "options": {
            "temperature": 0.0,
        },
    }

    data = json.dumps(payload).encode("utf-8")
    req = urllib.request.Request(
        OLLAMA_URL,
        data=data,
        headers={"Content-Type": "application/json"},
        method="POST",
    )

    with urllib.request.urlopen(req, timeout=OLLAMA_TIMEOUT) as resp:
        raw = resp.read().decode("utf-8", errors="replace")

    obj = json.loads(raw)

    # /api/chat devuelve algo tipo: { "message": {"role":"assistant","content":"..."}, ... }
    content = ""
    if isinstance(obj, dict):
        msg = obj.get("message")
        if isinstance(msg, dict):
            content = msg.get("content", "")

    return (content or "").strip()

# ----------------------------- MAIN ------------------------------------

# Si está vacío, respondemos none
if not frase_norm:
    out("none", 0, "No he entendido la orden.")
    raise SystemExit(0)

# Intento con Ollama primero
try:
    respuesta = call_ollama(frase_norm)

    # Validación mínima: si no viene en formato ACTION=..., usamos fallback
    if not respuesta.startswith("ACTION="):
        a, c, m = fallback_rules(frase_norm)
        out(a, c, m)
    else:
        # A veces los modelos meten saltos de línea: nos quedamos con la primera
        respuesta = respuesta.splitlines()[0].strip()
        print(respuesta)

except (urllib.error.URLError, urllib.error.HTTPError, TimeoutError, json.JSONDecodeError):
    # Si Ollama no está levantado o hay error, fallback
    a, c, m = fallback_rules(frase_norm)
    out(a, c, m)