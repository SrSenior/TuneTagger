# Notas de desarrollo

En este documento iré registrando el proceso de desarrollo de TuneTagger.

Como este es mi primer proyecto trabajando con .NET y ASP.NET Core Web API, el contenido irá cambiando conforme avance el proyecto. La idea no es solamente documentar lo que se implementa en cada commit, sino también dejar anotaciones sobre cosas que aprenda o re-aprenda durante el proceso.

Este documento también funciona como una bitácora personal de aprendizaje. Por eso, algunas explicaciones pueden parecer obvias para alguien con más experiencia, pero las incluyo porque me ayudan a entender mejor las decisiones técnicas del proyecto y a poder consultar este material en el futuro.

El lenguaje utilizado no necesariamente será el más técnico posible, aunque intentaré mantenerlo claro, ordenado y útil. La intención es que sirva tanto como documentación personal como referencia general del progreso del proyecto.

También mantendré una versión en inglés de este documento, para que cualquier persona que revise el repositorio pueda entender el proceso de desarrollo aunque no hable español.

---

## Contexto general del proyecto

TuneTagger es una aplicación pensada para analizar archivos de audio locales, identificar la canción real y devolver metadatos útiles como título, artista, álbum, nivel de confianza y un nombre de archivo sugerido.

La idea principal del proyecto nace de un problema común al manejar archivos MP3 descargados o guardados localmente: muchos archivos tienen nombres poco claros, incompletos o incorrectos. Por ejemplo, un archivo podría llamarse algo como `Black Clover Opening 10 Full.mp3`, aunque la canción real tenga un artista y título específicos.

El objetivo inicial de TuneTagger es permitir este flujo:

```txt
archivo de audio local
→ generación de fingerprint
→ consulta a AcoustID
→ selección de la mejor coincidencia
→ respuesta limpia con metadatos
→ nombre de archivo sugerido
```

Por ahora, el proyecto se está construyendo como una aplicación local-first. Esto significa que la app está pensada para correr localmente y no como una plataforma pública donde los usuarios suben archivos a un servidor externo.

---

## Estado inicial del backend

Para comenzar, se implementó una API con ASP.NET Core Web API. Esta API escucha peticiones en `localhost`, lo que significa que durante el desarrollo corre únicamente en la computadora local.

Inicialmente se crearon endpoints básicos para validar que la API funcionara correctamente:

* `GET /api/health`: endpoint de prueba para confirmar que el backend está corriendo.
* `GET /api/tracks/mock-analysis`: endpoint de prueba que devuelve datos simulados de una canción.
* `POST /api/tracks/analyze`: endpoint principal para recibir un archivo de audio y analizarlo.

También se creó un modelo llamado `TrackAnalysisResult`, que representa la respuesta final del análisis de una canción. Este modelo incluye datos como:

* nombre original del archivo;
* título detectado;
* artista;
* álbum;
* nombre de archivo sugerido;
* confianza de la coincidencia;
* estado del análisis.

El uso de modelos ayuda a que la respuesta de la API sea más clara y estructurada, en vez de devolver objetos anónimos en todas partes.

---

## 24/06

Se añadió `fpcalc.exe` al flujo de desarrollo.

`fpcalc` es una herramienta de Chromaprint que permite generar una fingerprint acústica de un archivo de audio. Esta fingerprint es una representación del contenido sonoro del archivo, no simplemente del nombre del archivo o de sus metadatos.

La fingerprint generada por `fpcalc`, junto con la duración de la canción, se utiliza posteriormente para consultar AcoustID y buscar coincidencias en su base de datos.

Se agregó un README dentro de:

```txt
/backend/TuneTagger.Api/Tools
```

En ese README se documentan detalles importantes sobre `fpcalc`, principalmente su ubicación esperada dentro del proyecto y cómo configurarlo para desarrollo local.

También se modificó el archivo `.csproj` del backend para indicar que `fpcalc.exe` debe copiarse al directorio de salida durante la compilación.

Esto es necesario porque, al ejecutar el proyecto, .NET no corre directamente desde la raíz del proyecto, sino desde una carpeta de salida como:

```txt
backend/TuneTagger.Api/bin/Debug/net10.0/
```

Por eso, si `fpcalc.exe` solamente existe en la carpeta `Tools` original, la aplicación podría no encontrarlo al ejecutarse. Para evitar ese problema, se configuró el proyecto para copiarlo a:

```txt
backend/TuneTagger.Api/bin/Debug/net10.0/Tools/fpcalc.exe
```

Esto permite usar rutas relativas durante el desarrollo y evita depender de una ruta absoluta específica de mi computadora.

---

## 25/06

Se agregó configuración para AcoustID.

La URL base de AcoustID se colocó en `appsettings.Development.json`, ya que es una configuración pública y puede cambiar dependiendo del ambiente.

En cambio, la API key de AcoustID no se colocó directamente en ningún archivo de configuración del repositorio, porque es un dato sensible que no debe subirse a GitHub.

Para desarrollo local se decidió usar `dotnet user-secrets`. Esta herramienta permite guardar secretos de desarrollo fuera del repositorio, evitando exponer claves privadas accidentalmente.

La idea general queda así:

* configuración pública: `appsettings.Development.json`;
* secretos locales: `dotnet user-secrets`;
* Docker o producción en el futuro: variables de entorno.

Esta separación es importante porque evita mezclar configuración normal con información sensible.

---

## 26/06

Se conectó el backend con AcoustID.

El endpoint principal ya no solamente recibe el archivo, sino que ahora realiza el flujo real de análisis:

```txt
archivo recibido
→ guardado temporal
→ ejecución de fpcalc
→ obtención de duration y fingerprint
→ consulta a AcoustID
→ lectura de respuesta JSON
```

También se implementó el envío de la solicitud POST a AcoustID usando compresión GZip. Esto se hizo porque las fingerprints pueden ser bastante largas, y AcoustID recomienda comprimir este tipo de solicitudes.

La solicitud enviada a AcoustID incluye datos como:

* `client`: API key de la aplicación;
* `duration`: duración del archivo de audio;
* `fingerprint`: fingerprint generada por `fpcalc`;
* `meta`: datos que queremos recibir de vuelta;
* `format`: formato de respuesta, en este caso JSON.

También se empezó a procesar la respuesta de AcoustID para seleccionar únicamente la mejor coincidencia, usando el valor `score` que devuelve la API.

El `score` representa qué tan fuerte es la coincidencia encontrada. Por eso, si AcoustID devuelve varios resultados, la app selecciona el resultado con mayor confianza.

---

## 27/06

Se inició la modularización del código.

Hasta este punto, gran parte de la lógica estaba dentro de `Program.cs`. Esto funcionaba para validar rápidamente el flujo, pero hacía que el archivo creciera demasiado y mezclara varias responsabilidades.

Se decidió separar parte de la lógica en servicios.

La idea principal fue dividir el backend de esta manera:

```txt
Program.cs
→ define endpoints y respuestas HTTP

FingerprintService
→ ejecuta fpcalc y obtiene duration + fingerprint

AcoustIdService
→ consulta AcoustID y obtiene la mejor coincidencia
```

Esta separación permite que cada clase tenga una responsabilidad más clara.

`FingerprintService` se encarga solamente de trabajar con `fpcalc`. No necesita saber nada sobre AcoustID ni sobre cómo se devuelve una respuesta HTTP.

`AcoustIdService` se encarga solamente de comunicarse con AcoustID. No necesita saber cómo se subió el archivo ni cómo se generó la fingerprint.

`Program.cs` queda como el punto donde se reciben las peticiones HTTP, se llaman los servicios necesarios y se devuelve una respuesta al cliente.

También se consideró crear un `TrackAnalysisService` para orquestar todo el flujo completo, pero por ahora se decidió mantener la coordinación en `Program.cs` para no agregar una capa extra demasiado pronto.

---

## 28/06

Se terminó de modularizar la lógica principal de AcoustID.

Dentro de `AcoustIdService` se separaron responsabilidades internas en métodos más pequeños. En particular, se separó la lógica de compresión GZip y la lógica encargada de extraer la mejor coincidencia desde los resultados devueltos por AcoustID.

Esto ayuda a que el método principal sea más fácil de leer, ya que no tiene que contener todos los detalles técnicos en un solo bloque enorme.

La estructura general quedó así:

```txt
FindBestMatchAsync
→ prepara datos
→ crea solicitud comprimida
→ envía solicitud a AcoustID
→ valida respuesta
→ obtiene results
→ extrae mejor coincidencia
```

También se decidió que `AcoustIdService` devuelva `null` cuando AcoustID no encuentra una coincidencia útil. Esto es mejor que devolver un resultado falso con valores como `"Unknown"` o `"No match found"`.

La lógica queda más clara:

```txt
AcoustIdBestMatch
→ sí se encontró una coincidencia útil

null
→ no se encontró una coincidencia útil

throw
→ ocurrió un error técnico
```

Ejemplos de errores técnicos serían:

* falta la URL base de AcoustID;
* falta la API key;
* AcoustID responde con un error HTTP;
* la respuesta JSON no tiene el formato esperado.

Con esto, `Program.cs` puede distinguir mejor entre un archivo no reconocido y un error real del sistema.

También se actualizó el estado final del análisis para usar un valor más claro como `matched` cuando sí se encuentra una coincidencia.

---

## Cierre de Etapa A: Backend funcional

Con estos cambios, se considera concluida la Etapa A del roadmap.

La Etapa A tenía como objetivo lograr que el backend pudiera analizar una canción individual de forma funcional.

Actualmente el backend ya puede:

* recibir un archivo de audio mediante `POST /api/tracks/analyze`;
* validar extensiones permitidas;
* guardar el archivo temporalmente;
* ejecutar `fpcalc`;
* obtener `duration` y `fingerprint`;
* consultar AcoustID;
* comprimir la solicitud POST con GZip;
* procesar la respuesta JSON;
* seleccionar la coincidencia con mayor confianza;
* devolver una respuesta limpia al frontend;
* generar un `suggestedFileName`;
* eliminar el archivo temporal después del análisis.

Esto no significa que el backend esté terminado en términos absolutos, pero sí significa que el flujo principal del MVP ya funciona.

Quedan mejoras posibles para más adelante, como:

* manejo de errores más detallado;
* uso de `IHttpClientFactory`;
* tests básicos;
* procesamiento por lotes;
* escritura de tags ID3;
* renombrado real de archivos;
* integración con cola de trabajos usando RabbitMQ;
* empaquetado con Docker.

Sin embargo, ninguna de esas mejoras bloquea el inicio de la siguiente fase.

---

## Etapa B: Frontend funcional

Después de completar el backend principal, se inició la Etapa B del roadmap.

El objetivo de esta etapa es construir una interfaz web local que permita interactuar con TuneTagger sin tener que usar `curl` o probar la API manualmente desde la terminal.

El frontend será construido con:

* Vite;
* React;
* TypeScript;
* pnpm como administrador de paquetes;
* Tailwind CSS para estilos.

Se creó el proyecto frontend con Vite y la plantilla de React + TypeScript. También se decidió usar `pnpm` en lugar de `npm` como administrador de paquetes.

La estructura inicial del frontend quedó dentro de:

```txt
frontend/tunetagger-web
```

También se instaló Tailwind CSS y se configuró correctamente con Vite. La prueba inicial confirmó que Tailwind funciona, ya que fue posible aplicar clases de utilidad directamente en `App.tsx`.

Por ahora, el objetivo inicial del frontend será mantenerlo simple:

```txt
seleccionar archivo
→ presionar botón de análisis
→ enviar archivo al backend
→ mostrar estado del análisis
→ mostrar resultado
```

Las funcionalidades iniciales esperadas son:

* pantalla para seleccionar un archivo de audio;
* botón para analizar el archivo;
* visualización del estado actual;
* manejo de estados como `idle`, `analyzing`, `matched`, `not-found` y `error`;
* visualización de título, artista, álbum, confianza y nombre sugerido;
* interfaz simple, local y funcional.

La prioridad de esta etapa no será crear una interfaz visualmente perfecta desde el inicio, sino lograr que el frontend se conecte correctamente con el backend y permita usar el flujo principal de TuneTagger desde el navegador.
