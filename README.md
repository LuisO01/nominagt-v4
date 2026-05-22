# NominaGT v4

Sistema empresarial de nóminas para Guatemala con cumplimiento del Código de Trabajo, validadores RENAP/SAT, dashboards interactivos, recibos PDF y envío automático por correo.

**Stack:** Oracle XE 21c + .NET 8 Web API + React 18 (Vite) + Power BI

![License](https://img.shields.io/badge/license-MIT-blue.svg)
![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)
![React](https://img.shields.io/badge/React-18.3-blue.svg)
![Oracle](https://img.shields.io/badge/Oracle-XE%2021c-red.svg)

---

## ✨ Features

### Módulos funcionales
- **Empleados**: CRUD completo con búsqueda, filtro por estado, indicador de acceso al sistema.
- **Nómina**: Periodos MENSUAL / QUINCENAL / **BONO14 / AGUINALDO** con cálculo proporcional automático.
- **Liquidación / Finiquito**: cálculo legal de indemnización + bono 14 + aguinaldo + vacaciones no gozadas con PDF de comprobante.
- **Vacaciones**: solicitudes, aprobación, saldo automático (15 días/año art. 130 CT).
- **Reportes**: Excel con 6 hojas estilo dashboard ejecutivo + PDF con resumen + preview interactivo en la UI.
- **Dashboard**: KPIs en vivo con auto-refresh, gráfica anual, comparativo con mes anterior.
- **Mis boletas**: portal del empleado para ver sus recibos individuales y descargar PDF.

### Compliance Guatemala
- ✅ **Validador DPI** con algoritmo RENAP (dígito verificador + código de depto 01-22).
- ✅ **Validador NIT** con módulo 11 SAT (soporta dígito K).
- ✅ **IGSS laboral 4.83%** + **IGSS patronal 10.67%**.
- ✅ **ISR progresivo** (5% hasta Q300k, 7% sobre el excedente).
- ✅ **Bonificación incentivo Q250** (Decreto 78-89, exenta de IGSS e ISR).
- ✅ **Salario mínimo 2026** Q3,591.85 actualizado.
- ✅ **Bono 14** (Decreto 42-92) y **Aguinaldo** (art. 137 CT) proporcionales.
- ✅ Validación de **teléfonos GT** (8 dígitos, prefijo 2-7).

### Roles y seguridad
- 5 roles (ADMIN / RRHH / NOMINA / EMPLEADO / AUDITOR) con permisos granulares.
- JWT + refresh tokens, bloqueo automático tras intentos fallidos.
- Cada empleado puede tener cuenta de acceso vinculada (creación automática al alta).
- Reset y reenvío de credenciales por email.

### Reportes y exportación
- **Excel** con 6 hojas (Dashboard, Detalle, Por departamento, Por puesto, Cargas patronales, Tendencia anual) — con data bars, color scales, icon sets, sparklines y hipervínculos entre hojas.
- **PDF** ejecutivo de la planilla mensual.
- **PDF de recibo individual** por empleado.
- **PDF de finiquito** al dar de baja.
- **Envío por email** de cualquier reporte o recibo con plantilla corporativa (vía SMTP).

---

## 📁 Estructura del proyecto

```
NominaGT_v4/
├── 01_database/      Scripts SQL (esquema + datos + migraciones)
├── 02_backend/       API REST .NET 8 (Dapper, BCrypt, JWT, QuestPDF, ClosedXML, MailKit)
├── 03_frontend/      SPA React + Vite + Tailwind
├── 04_powerbi/       Guía de conexión Oracle → Power BI + medidas DAX
├── scripts/          PowerShell para setup e inicio
└── README.md         Este archivo
```

---

## 🔧 Requisitos previos

| Software | Versión | Para qué |
|---|---|---|
| Oracle XE | 21c | Base de datos |
| .NET SDK | 8.0+ | Backend |
| Node.js | 18+ | Frontend |
| sqlplus o SQL Developer | — | Ejecutar scripts SQL |
| Visual Studio 2022 o VS Code | — | Desarrollo (opcional) |
| Power BI Desktop | Última | Reportes (opcional) |

---

## 🚀 Instalación paso a paso

### 1. Verificar que Oracle XE está corriendo (PowerShell como Admin)

```powershell
Get-Service "OracleService*", "OracleListener*" | Format-Table Name, Status

# Si alguno está Stopped:
Start-Service OracleServiceXE
Start-Service OracleOraDB21Home1TNSListener
```

### 2. Crear el esquema y cargar datos

```powershell
cd scripts
.\Setup-Database.ps1
```

Pedirá la password de **SYSTEM** (la que pusiste al instalar Oracle XE) y ejecutará en orden los scripts SQL.

### 3. Aplicar los scripts adicionales (orden importante)

Después de `Setup-Database.ps1` aplica estos scripts contra el usuario `nominagt` (en SQL Developer o sqlplus):

| Script | Para qué |
|---|---|
| `12_seed_users_real_hashes.sql` | Hashes BCrypt reales para los usuarios demo |
| `13_fix_views_ora01821.sql` | Fix de formato de fecha en vistas BI |
| `15_link_usuario_empleado.sql` | Vincula usuarios demo a empleados reales |
| `16_fix_calculo_nomina.sql` | SP de cálculo de nómina con MENSUAL/QUINCENAL/BONO14/AGUINALDO |
| `17_vacaciones.sql` | Módulo de vacaciones con vista de saldo |
| `18_fix_view_nomina_mensual.sql` | Vista BI con BONIFICACION e IGSS/ISR/Prestamos calculados |
| `19_liquidaciones.sql` | Tabla de liquidaciones / finiquito |
| `20_email_no_unique.sql` | Permite email duplicado entre cuentas |

### 4. Configurar SMTP (opcional pero recomendado)

Crear `02_backend/NominaGT.API/appsettings.Development.json` (excluido del git) con:

```json
{
  "Email": {
    "Smtp": {
      "Host": "smtp.gmail.com",
      "Port": 587,
      "UseStartTls": true,
      "Username": "tu-correo@gmail.com",
      "Password": "tu-app-password-16-chars",
      "FromName": "NominaGT v4",
      "FromAddress": "tu-correo@gmail.com"
    }
  }
}
```

> Para Gmail: activa verificación en 2 pasos y genera un **App Password** en https://myaccount.google.com/apppasswords.

### 5. Iniciar la aplicación

**Opción A — Script todo en uno:**
```powershell
.\scripts\Start-All.ps1
```

**Opción B — Dos terminales:**
```powershell
# Terminal 1 (Backend)
cd 02_backend\NominaGT.API
dotnet run

# Terminal 2 (Frontend)
cd 03_frontend
npm install   # solo la primera vez
npm run dev
```

URLs:
- Backend + Swagger: `https://localhost:5001/swagger`
- Frontend: `http://localhost:5173`

### 6. Login

| Usuario | Password | Rol | Notas |
|---|---|---|---|
| `admin` | `admin` | ADMIN | Acceso total. Usa bypass de seguridad — cámbialo en producción. |
| `rrhh.op1` | `rrhh123` | RRHH | Gestión de empleados y vacaciones |
| `nomina.op1` | `nomina123` | NÓMINA | Cálculo, aprobación, reportes |
| `empleado` | `empleado123` | EMPLEADO | Mis boletas, mi perfil, mis vacaciones |

---

## 🌐 Endpoints clave de la API

### Auth
| Método | Endpoint | Descripción |
|---|---|---|
| POST | `/api/auth/login` | Login (devuelve JWT + refresh token) |
| POST | `/api/auth/refresh` | Renovar token |

### Empleados
| Método | Endpoint | Auth |
|---|---|---|
| GET / POST / PUT | `/api/empleados/{id?}` | Variado |
| PATCH | `/api/empleados/{id}/estado` | ADMIN |
| POST | `/api/empleados/{id}/crear-acceso` | ADMIN/RRHH |
| POST | `/api/empleados/{id}/resetear-password` | ADMIN/RRHH |
| POST | `/api/empleados/{id}/liquidacion-preview` | ADMIN/RRHH |
| POST | `/api/empleados/{id}/liquidar` | ADMIN |
| GET | `/api/empleados/liquidaciones/{id}/pdf` | ADMIN/RRHH |
| GET | `/api/empleados/me/boletas` | * |

### Nómina
| Método | Endpoint |
|---|---|
| GET / POST | `/api/nomina/periodos` |
| POST | `/api/nomina/periodos/{id}/calcular` |
| POST | `/api/nomina/periodos/{id}/aprobar` |
| GET | `/api/nomina/recibo/{id}` |
| GET | `/api/nomina/recibo/{id}/pdf` |
| POST | `/api/nomina/recibo/{id}/email` |

### Reportes
| Método | Endpoint |
|---|---|
| GET | `/api/reportes/excel-mensual?anio=&mes=&tipoPeriodo=` |
| GET | `/api/reportes/pdf-mensual?anio=&mes=&tipoPeriodo=` |
| GET | `/api/reportes/nomina-mensual` (JSON, para preview) |
| POST | `/api/reportes/enviar-email` |

### Vacaciones
| Método | Endpoint |
|---|---|
| GET / POST | `/api/vacaciones` |
| GET | `/api/vacaciones/saldos` |
| GET | `/api/vacaciones/saldo?empleadoId=` |
| PATCH | `/api/vacaciones/{id}/estado` |

### Otros
- `/api/dashboard/kpis` y `/api/dashboard/resumen-anual` — datos del Dashboard
- `/api/catalogos/departamentos` y `/api/catalogos/puestos`
- `/api/auditoria` (ADMIN/AUDITOR) — bitácora

Documentación interactiva completa: `https://localhost:5001/swagger`

---

## 🐛 Troubleshooting

### "Cannot connect to Oracle"
- Verificá listener en puerto 1521: `tnsping XEPDB1`
- Password del usuario `nominagt`: la default es `NominaGT2026` (sin `#`). Si se bloqueó la cuenta, ejecuta `01_database/14_rescate_usuario_nominagt.sql` como `SYSTEM`.

### "Login fallido"
- Solo `admin/admin` funciona sin aplicar el seed (por el bypass de emergencia).
- Para que los demás usuarios funcionen, aplica `01_database/12_seed_users_real_hashes.sql`.

### "Error al cargar KPIs" o "ORA-01821"
- Aplica `01_database/13_fix_views_ora01821.sql`.

### Excel/PDF da error "ORA-00904: DESCUENTO_PRESTAMOS"
- Aplica `01_database/18_fix_view_nomina_mensual.sql`.

### "SMTP no está configurado"
- Crea `appsettings.Development.json` con la config SMTP (sección 4 de la guía).

### "Certificate error" en HTTPS
- `dotnet dev-certs https --trust`

---

## 🧪 Tecnologías por capa

**Backend:**
- ASP.NET Core 8.0 + Dapper 2.1
- Oracle.ManagedDataAccess.Core 23.5
- BCrypt.Net-Next 4.0 + JWT Bearer
- FluentValidation 11.3
- ClosedXML 0.104 (Excel)
- QuestPDF 2024.7 (PDF)
- MailKit 4.7 (SMTP)

**Frontend:**
- React 18.3 + Vite 5
- TailwindCSS 3.4
- React Router 6
- Axios con interceptors (auto-refresh JWT)
- Lucide React (íconos)

**Base de datos:**
- Oracle XE 21c
- 28+ tablas, 6 vistas Power BI
- Validadores GT: DPI (RENAP), NIT (SAT), teléfono, IGSS
- Cálculo automático de bono 14 / aguinaldo proporcionales

---

## 📊 Power BI (opcional)

Ver guía completa en `04_powerbi/conexion_oracle.md`.

Resumen:
1. Instalar Oracle Instant Client 21.x
2. Power BI Desktop → Obtener datos → Oracle
3. Servidor: `localhost:1521/xepdb1` · Usuario: `nominagt` · Password: `NominaGT2026`
4. Cargar las 6 vistas `VW_PBI_*`
5. Crear medidas DAX desde `04_powerbi/medidas_dax.txt`

---

## 🔒 Para producción

- ⚠️ Cambia `Jwt.Key` en `appsettings.json` por una clave segura.
- ⚠️ Elimina el bypass de `admin/admin` en `AuthService.cs`.
- ⚠️ Usa HTTPS válido (no `dotnet dev-certs`).
- ⚠️ Rota el App Password de SMTP regularmente.

---

## 📜 Licencia

MIT.

---

## 👤 Autor

**Estanly** — Universidad Mariano Gálvez de Guatemala
Curso: Gestión de Proyectos / 9° ciclo Ingeniería en Sistemas
