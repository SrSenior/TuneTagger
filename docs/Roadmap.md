# TuneTagger - Roadmap técnico

Este documento define la planificación general del proyecto TuneTagger, incluyendo las fases de desarrollo previstas desde el núcleo funcional de la aplicación hasta posibles extensiones avanzadas como Docker, RabbitMQ, observabilidad con Grafana, integración con modelos de IA y despliegue con Kubernetes.

## Visión general

TuneTagger inicia como una aplicación local-first para identificar y organizar archivos de audio, y evoluciona hacia una arquitectura más completa con procesamiento asíncrono, observabilidad y enriquecimiento inteligente de metadatos.

El objetivo principal no es agregar tecnologías de forma forzada, sino incorporarlas cuando tengan una función clara dentro del proyecto.

---

## Etapa A: Núcleo funcional

**Objetivo:** lograr que TuneTagger funcione de verdad con una canción individual.

Esta etapa se enfoca en construir el flujo principal de análisis musical:

```txt
Archivo de audio
↓
Extracción de fingerprint
↓
Consulta a AcoustID
↓
Obtención de metadatos
↓
Generación de nombre sugerido
```

### Avances actuales

- API en ASP.NET Core creada.
- Endpoint `/api/health` implementado.
- Endpoint `/api/tracks/mock-analysis` implementado.
- Endpoint `POST /api/tracks/analyze` capaz de recibir archivos.
- `fpcalc.exe` ubicado en la carpeta `Tools/`.
- `fpcalc.exe` probado manualmente desde la terminal con un archivo MP3.
- Ejecutar `fpcalc` desde C#.
- Obtener `duration` y `fingerprint`.
- Consultar AcoustID usando esos datos.
- Procesar la respuesta de AcoustID.
- Seleccionar la coincidencia con mayor confianza.
- Devolver una respuesta limpia al frontend.
- Generar un `suggestedFileName`.

### Próximos pasos

Ninguno, se concluyó la Etapa A: Backemd :)

---

## Etapa B: Frontend funcional

**Objetivo:** permitir que el usuario interactúe con TuneTagger desde una interfaz web local.

El frontend será construido con Vite, React y TypeScript.

### Funcionalidades iniciales

- Pantalla para seleccionar un archivo de audio.
- Botón para analizar el archivo.
- Visualización del resultado.
- Mostrar título, artista, álbum, confianza y nombre sugerido.
- Estado del análisis: recibido, procesando, encontrado, error.
- Botones iniciales para aceptar o cancelar el resultado.

### Propósito

Esta etapa convierte el backend funcional en una aplicación usable, aunque todavía no aplique cambios reales sobre los archivos.

---

## Etapa C: Aplicar cambios locales

**Objetivo:** permitir que TuneTagger modifique archivos locales únicamente con confirmación del usuario.

### Funcionalidades previstas

- Renombrar archivos de audio usando el nombre sugerido.
- Permitir edición manual antes de aplicar el cambio.
- Evitar sobrescritura de archivos existentes.
- Manejar errores de permisos o rutas inválidas.
- Agregar escritura de metadatos ID3 en una fase posterior.

### Posibles metadatos a modificar

- Título.
- Artista.
- Álbum.
- Género.
- Año.
- Número de pista.

### Consideraciones

La modificación de archivos debe hacerse de forma segura. TuneTagger no debe renombrar ni editar metadatos automáticamente sin que el usuario confirme primero.

---

## Etapa D: Docker

**Objetivo:** contenerizar el backend para que pueda ejecutarse en un ambiente reproducible.

Docker permitirá empaquetar la API junto con sus dependencias principales.

### Propósito

- Evitar problemas de configuración local.
- Hacer que el backend sea más fácil de ejecutar en otros entornos.
- Incluir la dependencia de fingerprinting en el contenedor.
- Preparar el proyecto para una arquitectura más avanzada.

### Consideración importante sobre `fpcalc`

En desarrollo local sobre Windows se utiliza:

```txt
Tools/fpcalc.exe
```

Sin embargo, dentro de un contenedor Linux se utilizaría una ruta diferente, por ejemplo:

```txt
/usr/bin/fpcalc
```

Por esta razón, la ruta de `fpcalc` debe manejarse mediante configuración y no quedar fija en el código.

Ejemplo conceptual:

```txt
Windows:
FpcalcPath = Tools/fpcalc.exe

Docker/Linux:
FpcalcPath = /usr/bin/fpcalc
```

---

## Etapa E: Docker Compose

**Objetivo:** levantar varios servicios del proyecto de forma coordinada.

Docker Compose permitirá ejecutar varios componentes con un solo comando.

### Servicios posibles

- API de TuneTagger.
- Worker de procesamiento.
- RabbitMQ.
- Prometheus.
- Grafana.

### Propósito

Esta etapa facilitará el desarrollo de una arquitectura distribuida local, permitiendo levantar todo el stack con:

```bash
docker compose up
```

---

## Etapa F: RabbitMQ y worker

**Objetivo:** implementar procesamiento asíncrono para análisis por lotes.

RabbitMQ será utilizado como message broker para manejar trabajos pendientes de análisis.

### Flujo propuesto

```txt
Frontend
↓
API recibe archivos
↓
API crea trabajos
↓
RabbitMQ guarda trabajos en cola
↓
Worker procesa cada trabajo
↓
Frontend consulta el estado
```

### Responsabilidades

#### API

- Recibir archivos o solicitudes de análisis.
- Crear trabajos.
- Enviar mensajes a RabbitMQ.
- Exponer endpoints para consultar estado.

#### RabbitMQ

- Almacenar trabajos pendientes.
- Entregar trabajos a los workers.
- Permitir procesamiento desacoplado.

#### Worker

- Tomar trabajos desde RabbitMQ.
- Ejecutar `fpcalc`.
- Consultar AcoustID.
- Procesar resultados.
- Marcar trabajos como completados o fallidos.

### Estados posibles de un trabajo

```txt
pending
processing
completed
failed
```

### Justificación

RabbitMQ no es necesario para analizar una sola canción, pero sí tiene sentido cuando TuneTagger procese múltiples archivos o carpetas completas.

---

## Etapa G: Observabilidad con Grafana

**Objetivo:** monitorear el comportamiento interno del sistema.

Grafana se utilizará para visualizar métricas del proyecto, posiblemente recolectadas mediante Prometheus.

### Métricas útiles

```txt
tunetagger_tracks_analyzed_total
tunetagger_fingerprint_errors_total
tunetagger_acoustid_requests_total
tunetagger_acoustid_failures_total
tunetagger_analysis_duration_seconds
tunetagger_jobs_pending
tunetagger_jobs_completed_total
tunetagger_jobs_failed_total
```

### Dashboards posibles

- Canciones analizadas por minuto.
- Errores de `fpcalc`.
- Tiempo promedio de análisis.
- Trabajos pendientes en RabbitMQ.
- Errores de AcoustID.
- Cantidad de trabajos completados.
- Cantidad de trabajos fallidos.

### Propósito

Esta etapa busca agregar visibilidad al sistema y demostrar prácticas de observabilidad aplicadas a una aplicación real.

---

## Etapa H: Integración con IA / Hugging Face

**Objetivo:** enriquecer metadatos musicales usando modelos de IA de forma complementaria.

La IA no será parte del flujo principal de identificación, sino una capa adicional para mejorar la organización de la biblioteca musical.

### Feature propuesta: AI Metadata Assistant

A partir de datos como título, artista y álbum, el asistente podría sugerir:

- Mood.
- Etiquetas de biblioteca.
- Idioma probable.
- Categoría musical aproximada.
- Nombre limpio del archivo.
- Si la canción parece opening, ending, remix, cover, live version, etc.

### Ejemplo de salida

```json
{
  "languageGuess": "Japanese / English",
  "moodTags": ["energetic", "funk", "anime opening"],
  "libraryTags": ["anime", "jazz-funk", "opening"],
  "cleanFileName": "ALI - Wild Side.mp3"
}
```

### Consideraciones

No se plantea usar IA para obtener o mostrar letras completas de canciones, ya que eso puede involucrar problemas de derechos de autor. La integración de IA debe enfocarse en enriquecer, clasificar o limpiar metadatos.

---

## Etapa I: Kubernetes

**Objetivo:** crear una demostración de despliegue avanzado usando contenedores.

Kubernetes se considerará como una fase opcional y final, una vez que el proyecto ya tenga Docker, Docker Compose, RabbitMQ y observabilidad funcionando.

### Componentes posibles

- Deployment para la API.
- Deployment para el worker.
- Service para la API.
- Service para RabbitMQ.
- Configuración para Prometheus.
- Configuración para Grafana.

### Propósito

Kubernetes no es necesario para el MVP, pero puede servir como demostración de conocimientos de despliegue, escalabilidad y orquestación de contenedores.

---

## Orden recomendado de implementación

```txt
1. Terminar ejecución de fpcalc desde C#.
2. Integrar AcoustID.
3. Devolver resultado real de análisis.
4. Crear frontend con Vite, React y TypeScript.
5. Mostrar resultados visualmente.
6. Permitir confirmación de cambios.
7. Renombrar archivos.
8. Escribir metadatos ID3.
9. Dockerizar backend.
10. Crear docker-compose básico.
11. Implementar procesamiento por lotes.
12. Agregar RabbitMQ y worker.
13. Agregar métricas.
14. Visualizar métricas con Grafana.
15. Agregar feature de IA.
16. Agregar Kubernetes como fase opcional.
```

---

## Principio guía

El proyecto debe crecer por necesidad técnica, no por acumulación artificial de herramientas.

Cada tecnología debe responder a una pregunta concreta:

```txt
.NET:
¿Cómo construyo una API local robusta?

Chromaprint / fpcalc:
¿Cómo obtengo una huella acústica del archivo?

AcoustID:
¿Cómo identifico la canción a partir de la huella acústica?

React / Vite:
¿Cómo construyo una interfaz web local usable?

Docker:
¿Cómo ejecuto el backend en un ambiente reproducible?

RabbitMQ:
¿Cómo proceso múltiples archivos sin bloquear la API?

Grafana:
¿Cómo observo errores, tiempos y volumen de procesamiento?

Hugging Face:
¿Cómo enriquezco los metadatos de forma inteligente?

Kubernetes:
¿Cómo demuestro despliegue y orquestación de servicios?
```

---