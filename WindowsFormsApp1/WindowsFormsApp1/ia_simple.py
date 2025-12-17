# -*- coding: utf-8 -*-
import sys
import re

print("IA INICIADA")

# Frase original
frase = sys.argv[1].lower()

# Normalizar: quitar signos y espacios raros
frase = re.sub(r'[^\w\s]', '', frase).strip()

# 🔹 RESPUESTAS PRIORITARIAS
if frase == "hola":
    print("Hola, estoy lista para ayudarte.")

elif frase.startswith("hola"):
    print("Hola, dime qué necesitas.")

# 🔹 COMANDOS
elif "despeg" in frase:
    print("¿Estás segura de que quieres que despegue?")

elif frase == "conectar":
    print("Intentando conectar.")

# 🔹 POR DEFECTO
else:
    print("Te he escuchado.")
