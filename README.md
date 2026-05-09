# TuneTagger

Aplicación local-first para identificar, organizar y renombrar archivos de audio utilizando fingerprinting acústico y consulta de metadatos musicales.

El objetivo del proyecto es permitir que el usuario analice archivos de audio locales, obtenga información más precisa sobre la canción, artista, álbum y otros metadatos, y pueda generar nombres de archivo más ordenados sin necesidad de subir sus archivos completos a un servidor externo.

## Descripción

TuneTagger nace como una herramienta para organizar bibliotecas musicales locales cuyos archivos tienen nombres poco descriptivos, incompletos o incorrectos.

Por ejemplo, archivos con nombres como:

```txt
Black Clover Opening 10 Full.mp3
anime song final version.mp3
track_01.mp3
```

podrían ser analizados para obtener una sugerencia más clara como:

```txt
Vickeblanka - Black Catcher.mp3
```

La aplicación está pensada para trabajar con archivos de audio que el usuario tenga disponibles localmente. El procesamiento principal ocurre en la máquina del usuario, evitando la necesidad de alojar o almacenar archivos de audio completos en un servidor externo.

## Objetivo del proyecto

El objetivo principal es construir una aplicación local con interfaz web que permita:

- Leer archivos de audio desde el equipo del usuario.
- Generar un fingerprint acústico del archivo.
- Consultar servicios externos para identificar la canción.
- Obtener metadatos como título, artista, álbum y duración.
- Proponer un nuevo nombre para el archivo.
- Permitir que el usuario revise, edite o rechace los resultados antes de aplicar cambios.
- Guardar un historial local de archivos analizados.

## Arquitectura propuesta

El proyecto está planteado como una aplicación local-first con interfaz web.

```txt
Archivo de audio local
        ↓
Backend local en ASP.NET Core
        ↓
Generación de fingerprint con Chromaprint/fpcalc
        ↓
Consulta a AcoustID / MusicBrainz
        ↓
Resultados en interfaz web
        ↓
Usuario acepta, edita o rechaza cambios
```

La aplicación no está pensada como una web pública de subida de archivos. En su lugar, el usuario ejecuta la aplicación localmente y accede a una interfaz web desde su navegador.

## Tecnologías previstas

### Backend

- C#
- ASP.NET Core Web API
- Entity Framework Core
- SQLite
- Chromaprint / fpcalc
- AcoustID API
- MusicBrainz API

### Frontend

- React
- Vite
- TypeScript
- CSS / Tailwind CSS

### Herramientas

- .NET SDK
- Visual Studio Code
- Git / GitHub
- GitHub Releases

## Funcionalidades planeadas

### Versión inicial

- Selección de archivos de audio locales.
- Análisis individual de archivos.
- Generación de fingerprint acústico.
- Consulta de coincidencias en AcoustID.
- Visualización de posibles resultados.
- Sugerencia de nombre de archivo.
- Confirmación manual antes de realizar cambios.

### Funcionalidades futuras

- Procesamiento por lotes.
- Escaneo de carpetas completas.
- Edición manual de metadatos.
- Actualización de tags ID3.
- Historial de archivos procesados.
- Exportación de resultados en CSV o JSON.
- Detección de duplicados.
- Reglas personalizadas para nombres de archivo.
- Versión portable para Windows.

## Estado del proyecto

El proyecto se encuentra en fase inicial de desarrollo.

Actualmente se está definiendo la arquitectura base, el flujo principal de análisis de audio y la integración con servicios externos de identificación musical.

## Instalación para desarrollo

Esta sección se actualizará conforme avance el desarrollo del proyecto.

### Requisitos previstos

- .NET SDK
- Node.js
- Git
- Visual Studio Code o editor equivalente

### Clonar el repositorio

```bash
git clone URL_DEL_REPOSITORIO
cd TuneTagger
```

### Estructura esperada del proyecto

```txt
TuneTagger/
│
├── backend/
│   └── TuneTagger.Api/
│
├── frontend/
│   └── TuneTagger.Client/
│
├── docs/
│
└── README.md
```

### Ejecutar el backend

```bash
cd backend/TuneTagger.Api
dotnet run
```

### Ejecutar el frontend

```bash
cd frontend/TuneTagger.Client
npm install
npm run dev
```

## Uso previsto

Una vez ejecutada la aplicación, el usuario podrá abrir la interfaz web local desde el navegador, seleccionar archivos de audio y revisar los resultados del análisis.

Flujo básico esperado:

```txt
1. Ejecutar la aplicación localmente.
2. Seleccionar uno o varios archivos de audio.
3. Analizar los archivos.
4. Revisar las coincidencias encontradas.
5. Aceptar, editar o rechazar el nombre sugerido.
6. Aplicar los cambios seleccionados.
```

## Privacidad y procesamiento local

TuneTagger está pensado bajo un enfoque local-first.

Esto significa que los archivos de audio completos se procesan en el equipo del usuario. La aplicación no requiere subir canciones completas a un servidor externo para su análisis.

La consulta a servicios externos se realiza utilizando datos derivados del audio, como fingerprints acústicos y duración, con el objetivo de encontrar coincidencias y obtener metadatos musicales.

## Motivación

Este proyecto surge de una necesidad común al manejar bibliotecas de audio locales: muchos archivos tienen nombres incorrectos, incompletos o poco útiles, especialmente cuando provienen de distintas fuentes o fueron descargados con nombres genéricos.

Más allá de resolver ese problema, TuneTagger también funciona como un proyecto de aprendizaje y portafolio, integrando conceptos como:

- Desarrollo backend con .NET.
- Construcción de APIs.
- Procesamiento de archivos locales.
- Consumo de APIs externas.
- Manejo de bases de datos locales.
- Desarrollo frontend moderno.
- Diseño de una arquitectura local-first.
- Empaquetado de aplicaciones para distribución.

## Licencia

Este proyecto se desarrolla con fines educativos y de portafolio.

La licencia será definida más adelante.
