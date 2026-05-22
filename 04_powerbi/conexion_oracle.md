# Conexión de Power BI Desktop a NominaGT v4

## Requisitos previos

1. **Power BI Desktop** instalado (gratuito desde Microsoft Store)
2. **Oracle Data Access Components (ODAC)** o **Oracle Instant Client** instalado en la PC
   - Descargar desde: https://www.oracle.com/database/technologies/instant-client.html
   - Versión recomendada: Instant Client 21.x para Windows x64

## Paso 1 — Instalar Oracle Instant Client

1. Descargar `instantclient-basic-windows.x64-21.x.zip`
2. Extraer a `C:\oracle\instantclient_21_x`
3. Agregar esa ruta al PATH del sistema:
   - Panel de control → Sistema → Variables de entorno
   - Editar `PATH` → Nueva → `C:\oracle\instantclient_21_x`
4. Reiniciar la PC

## Paso 2 — Conectar Power BI Desktop

1. Abrir Power BI Desktop
2. **Inicio** → **Obtener datos** → **Más** → buscar "Oracle"
3. Seleccionar **Base de datos Oracle** → **Conectar**

4. En el cuadro de diálogo:
   ```
   Servidor:  localhost:1521/xepdb1
   Modo:      Importar (recomendado) o DirectQuery
   ```

5. Credenciales:
   ```
   Tipo:      Base de datos
   Usuario:   nominagt
   Password:  NominaGT2026#
   ```

6. **Aceptar** → Power BI lista todas las vistas y tablas

## Paso 3 — Cargar las vistas optimizadas

En el navegador de objetos, marcar SOLO las vistas con prefijo `VW_PBI_`:

| Vista | Para qué |
|---|---|
| `VW_PBI_EMPLEADOS` | Catálogo plano de empleados con datos del contrato |
| `VW_PBI_NOMINA_MENSUAL` | Detalle mes por mes (la principal para reportes) |
| `VW_PBI_RESUMEN_MENSUAL` | Totales agregados (KPIs) |
| `VW_PBI_HISTORICO_EMPLEADO` | Evolución salarial por empleado |
| `VW_PBI_DETALLE_CONCEPTOS` | Cada línea de ingreso/deducción |
| `VW_PBI_PRESTAMOS` | Cartera de préstamos vigentes |

Click **Cargar** o **Transformar datos** si querés modelar.

## Paso 4 — Modelar relaciones

Power BI suele detectar las relaciones por nombres. Si no, créalas manualmente:

```
VW_PBI_NOMINA_MENSUAL [empleado_id]    → VW_PBI_EMPLEADOS [empleado_id]   (N:1)
VW_PBI_HISTORICO_EMPLEADO [empleado_id] → VW_PBI_EMPLEADOS [empleado_id]   (N:1)
VW_PBI_DETALLE_CONCEPTOS [codigo_empleado] → VW_PBI_EMPLEADOS [codigo]     (N:1)
VW_PBI_PRESTAMOS [codigo_empleado]     → VW_PBI_EMPLEADOS [codigo]         (N:1)
```

## Paso 5 — Medidas DAX recomendadas

Crear estas medidas en `VW_PBI_NOMINA_MENSUAL`:

```dax
Total Empleados = DISTINCTCOUNT(VW_PBI_NOMINA_MENSUAL[empleado_id])

Total Neto = SUM(VW_PBI_NOMINA_MENSUAL[salario_neto])

Promedio Salario = AVERAGE(VW_PBI_NOMINA_MENSUAL[salario_neto])

% IGSS = DIVIDE(SUM(VW_PBI_NOMINA_MENSUAL[igss]),
               SUM(VW_PBI_NOMINA_MENSUAL[total_ingresos]))

YoY Neto =
VAR ActualMes = SUM(VW_PBI_NOMINA_MENSUAL[salario_neto])
VAR MesAnterior =
    CALCULATE(SUM(VW_PBI_NOMINA_MENSUAL[salario_neto]),
              DATEADD(VW_PBI_NOMINA_MENSUAL[fecha_pago], -1, YEAR))
RETURN DIVIDE(ActualMes - MesAnterior, MesAnterior)
```

## Paso 6 — Visualizaciones sugeridas

| Visual | Datos |
|---|---|
| Card | `Total Neto`, `Total Empleados` |
| Línea | `salario_neto` por `mes` (eje), `tipo_periodo` (leyenda) |
| Treemap | `total_neto` por `departamento` |
| Tabla | Detalle empleado con `salario_base`, `igss`, `isr`, `neto` |
| Slicer | `anio`, `mes`, `departamento`, `puesto` |

## Refresh automático

**Opción A — Manual:** Inicio → Actualizar (recarga todos los datos)

**Opción B — Programado:** Publicar a Power BI Service y configurar refresh cada N horas

**Opción C — DirectQuery:** Si elegiste DirectQuery en el paso 2, los datos se consultan en tiempo real (más lento pero siempre actualizado)

## Troubleshooting

**Error: "Oracle Client no encontrado"**
- Verifica que Oracle Instant Client está en el PATH
- Reinicia Power BI Desktop después de instalarlo

**Error: "ORA-12541: TNS:no listener"**
- El servicio Oracle XE no está corriendo
- Inicia el servicio "OracleServiceXE" desde services.msc

**Error: "ORA-01017: invalid username/password"**
- Password mal escrita. La password real está en `01_database/01_create_user.sql`
- Si la cambiaste, reflejá el cambio en Power BI

**Error: "Error 12154: TNS:could not resolve"**
- Probaste con `localhost/xepdb1`. Probá con `localhost:1521/xepdb1`

## Conexión alternativa: ODBC

Si Oracle Instant Client da problemas, podés usar ODBC:

1. Instalar el driver ODBC de Oracle
2. Configurar DSN en "ODBC Data Sources (64-bit)"
3. En Power BI: Obtener datos → ODBC → seleccionar el DSN
