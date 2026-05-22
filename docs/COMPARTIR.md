# Cómo compartir NominaGT con otras personas

Tu app corre localmente en tu PC con Oracle XE. Para que otras personas la puedan probar desde internet, necesitas exponer tu PC con un **túnel HTTPS**. Estas son las opciones recomendadas, de más simple a más profesional.

> **Importante**: tu PC tiene que estar **encendida** y con el backend corriendo para que la URL pública funcione. Si apagas la PC, la URL deja de responder.

---

## Antes de empezar (preparar la app)

Ya configuramos el backend para servir el frontend desde el mismo origen (puerto 5000). Cada vez que vayas a compartir:

```powershell
# 1. Compilar el frontend y copiarlo al backend
cd C:\Users\Luis Orozco\Downloads\NominaGT_v4\NominaGT_v4\scripts
.\Build-Deploy.ps1

# 2. Iniciar el backend (sirve UI + API en :5000)
cd ..\02_backend\NominaGT.API
dotnet run

# Verifica abriendo en tu navegador:
#   http://localhost:5000
# (debe cargar la pantalla de login)
```

Una vez que `localhost:5000` funciona, elegí UNA de las opciones de abajo para exponerlo.

---

## ⭐ Opción 1: ngrok (la más simple, 5 minutos)

**Pros:** Súper rápido, sin cuenta hace falta pero con cuenta gratis tienes URL fija.
**Contras:** El plan gratis tiene un banner inicial que el usuario debe aceptar.

### Instalación

1. Descargá ngrok desde https://ngrok.com/download (Windows installer)
2. Crea cuenta gratis en https://dashboard.ngrok.com/signup
3. Copia tu authtoken desde https://dashboard.ngrok.com/get-started/your-authtoken
4. En PowerShell:
   ```powershell
   ngrok config add-authtoken TU_TOKEN_AQUI
   ```

### Uso

```powershell
# Con el backend corriendo en localhost:5000, abre OTRA terminal:
ngrok http 5000
```

Verás algo así:
```
Session Status   online
Forwarding       https://abc123.ngrok-free.app -> http://localhost:5000
```

**La URL `https://abc123.ngrok-free.app` es la que compartes.** Pueden acceder desde cualquier dispositivo y país.

> La URL cambia cada vez que reinicias ngrok (en plan gratis). Si querés que sea fija, ngrok ofrece "static domain" gratis: https://dashboard.ngrok.com/cloud-edge/domains

---

## ⭐ Opción 2: Cloudflare Tunnel (gratis y URL fija)

**Pros:** Gratis con URL fija, sin banner, mejor rendimiento, sin límite de tráfico.
**Contras:** Setup inicial un poco más largo (~15 min).

### Instalación rápida (sin login, URL random)

```powershell
# Descarga cloudflared:
# https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe
# Renombrarlo a cloudflared.exe y poner en el PATH (o en C:\Windows)

# Con backend corriendo:
cloudflared tunnel --url http://localhost:5000
```

Te dará una URL tipo `https://something-random.trycloudflare.com`. Funciona inmediatamente.

### Con dominio fijo (recomendado para uso continuo)

Si tenés un dominio propio (o gratis tipo `.tk`, `.ml`):

1. `cloudflared login` → autoriza tu cuenta de Cloudflare
2. `cloudflared tunnel create nominagt`
3. Crear `config.yml`:
   ```yaml
   tunnel: nominagt
   credentials-file: C:\Users\TU_USUARIO\.cloudflared\xxx.json
   ingress:
     - hostname: nominagt.tudominio.com
       service: http://localhost:5000
     - service: http_status:404
   ```
4. `cloudflared tunnel route dns nominagt nominagt.tudominio.com`
5. `cloudflared tunnel run nominagt`

---

## ⭐ Opción 3: localtunnel (alternativa zero-config)

```powershell
# Instalar (una vez):
npm install -g localtunnel

# Usar:
lt --port 5000 --subdomain nominagt
```

Te da `https://nominagt.loca.lt`. Limitaciones: lentitud variable, pide aceptar warning la primera vez.

---

## ⭐ Opción 4: Deploy en cloud (más serio, sin necesidad de tu PC)

Si querés que la app esté disponible **sin depender de tu PC encendida**, necesitas:

| Componente | Servicio recomendado | Costo |
|---|---|---|
| Backend .NET | Render / Railway / Fly.io | Gratis con limitaciones |
| Frontend (ya integrado al back) | (mismo) | — |
| Oracle | **Oracle Cloud Always Free Autonomous DB** | Gratis para siempre |
| Email SMTP | Tu Gmail actual o SendGrid | Gratis |

> Oracle Cloud Always Free incluye **2 Autonomous Databases gratis para siempre** (cada una con 20 GB). Es la única forma viable de tener Oracle en cloud sin pagar. Requiere migrar el esquema desde XE → Autonomous, pero los scripts SQL son los mismos.

Si querés este camino, te puedo ayudar con la migración. Pero para una demo o pruebas con compañeros, ngrok o Cloudflare son suficientes.

---

## 🔒 Antes de compartir, considerá esto

1. **Tu BD Oracle local seguirá funcionando** — el backend se conecta a `localhost:1521` y los visitantes externos no acceden a la BD directamente, solo a través de tu API.

2. **Tus credenciales SMTP están en `appsettings.Development.json`** (no en el repo). Si la app envía correos, llegarán desde **tu Gmail** (`luisfer1250@gmail.com`).

3. **Cualquiera con la URL puede intentar loguear**. Considerá:
   - Cambiar el password de `admin` (no dejar `admin/admin` en una URL pública).
   - Aplicar `01_database/12_seed_users_real_hashes.sql` para que solo los usuarios demo con contraseñas reales funcionen.
   - O, mejor, crear cuentas específicas para las personas que vas a invitar y darles solo a ellas.

4. **Cierra el túnel cuando termines la demo** para no dejar tu PC expuesta indefinidamente.

5. **Logs**: cuando vean errores, los logs aparecen en tu consola del backend. Si la app cae, simplemente reinicia con `dotnet run`.

---

## Checklist rápido

- [ ] Backend buildeado con frontend integrado (`Build-Deploy.ps1`)
- [ ] Backend corriendo: `dotnet run` en `02_backend/NominaGT.API`
- [ ] Verifica `http://localhost:5000` carga la UI
- [ ] Inicia el túnel (ngrok / cloudflared / lt)
- [ ] Abre la URL pública en otro dispositivo y prueba login
- [ ] Si funciona, comparte la URL con tu grupo
