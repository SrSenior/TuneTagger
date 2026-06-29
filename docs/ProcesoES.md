# Notas de desarrollo

En este documento iré documentando el proceso de desarrollo. 

Puesto que es mi primer proyecto con .NET, el contenido irá cambiando con cada commit, iré indicando a detalle características/cosas que aprenda o re-aprenda conforme voy commiteando, esto no solo con fines documentales sino también para acudir al material de manera autodidáctica en caso de requerirlo a futuro.

Sobra indicar que el lenguaje utilizado no será el más técnico, aunque tampoco será vulgar.

De manera homóloga, haré una versión en inglés de este documento para cualquier lector que desee acudir al mismo.

Para empezar, se ha implementado una API que está escuchando peticiones, funciona en localhost y actualmente tiene dos endpoints funcionales, un método get que analiza una canción y devuelve los datos de la misma, y un método post donde el usuario sube un archivo mp3.

También se creó un modelo que funciona para almacenar los resultados del trackAnalysis

24/06
Se añadió fpcalc.exe, esta es una librería que nos da la fingerprint de las canciones, con las que luego vamos a consultar sus datos a AcoustID. En el readme ubicado en /backend/TuneTagger.Api/Tools se indican cosas importantes de fpcalc en relación con su ubicación principalmente.
En csproj añadimos una indicación de que fpcalc.exe se copiara a backend/TuneTagger.Api/bin/Debug/net10.0/Tools/fpcalc.exe porque sino daría error, al menos durante el desarrollo.

25/06
La URL base de AcoustID se colocó en `appsettings.Development.json` porque es configuración pública y puede cambiar entre ambientes. En cambio, la API key no se colocó en el archivo de configuración porque es un valor sensible que no debe subirse al repositorio. Para desarrollo local se usará `dotnet user-secrets`, y para Docker o producción se podrán usar variables de entorno.

26/06
Se conectó con AcoustID, además se hace la compresión de la solicitud POST con compresión con gzip, como lo recomienda AcosusID. También se escoge solamente la mejor coincidencia que devuelve la API.

27/06
Se modulariza el código. Por el momento la idea que se tiene es usar uno de las clases para hacer el uso de fpcalc y otra para pasar los datos a AcoustID (Y dentro de esta seccionarlo en 3: una función para compresión, una función para enviar la fingerprint a AcoustID y recibir la respuesta, y una función para limpiar el JSON que obtuvimos)

28/06
Hoy planeo hacer la segmentación de AcoustIdService.cs, es decir, tener una función para la compresión con gzip y una para limpiar el JSON que retorna AcoustID
Se modularizó lo que se tenía planeado y se concluye la Etapa A: Frontend I. Con esto podemos empezar la Etapa B: Frontend.
Con pnpm como administrador de paquetes se hizo la instalación de lo necesario para dar comienzo a la etapa B del proyecto.