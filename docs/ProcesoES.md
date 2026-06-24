# Notas de desarrollo

En este documento iré documentando el proceso de desarrollo. 

Puesto que es mi primer proyecto con .NET, el contenido irá cambiando con cada commit, iré indicando a detalle características/cosas que aprenda o re-aprenda conforme voy commiteando, esto no solo con fines documentales sino también para acudir al material de manera autodidáctica en caso de requerirlo a futuro.

Sobra indicar que el lenguaje utilizado no será el más técnico, aunque tampoco será vulgar.

De manera homóloga, haré una versión en inglés de este documento para cualquier lector que desee acudir al mismo.

Para empezar, se ha implementado una API que está escuchando peticiones, funciona en localhost y actualmente tiene dos endpoints funcionales, un método get que analiza una canción y devuelve los datos de la misma, y un método post donde el usuario sube un archivo mp3.

También se creó un modelo que funciona para almacenar los resultados del trackAnalysis