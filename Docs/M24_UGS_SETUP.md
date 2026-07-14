# M24 — Configuración de Unity Gaming Services

M24 no guarda claves, tokens ni datos de Relay en el repositorio. El proyecto actual no está vinculado a Unity Cloud (`cloudProjectId` vacío), por lo que Multijugador muestra **El proyecto online necesita configuración** y Local/Development Direct siguen disponibles.

1. Abre el proyecto en Unity y entra en **Edit > Project Settings > Services**.
2. Vincúlalo a la organización y al Unity Cloud Project correctos; confirma que aparece un Project ID.
3. En Unity Dashboard, abre ese proyecto y selecciona el entorno configurado en `Assets/Resources/Online/OnlineServicesSettings.asset`. En el proyecto actual es **production**; crea un entorno `development` y cambia ese asset sólo si quieres aislar las pruebas.
4. Activa **Authentication** y confirma Anonymous Sign-in.
5. Activa **Multiplayer Services** y comprueba que Relay está disponible en el mismo entorno.
6. Verifica cuotas y regiones Relay para pruebas.
7. Vuelve a Unity, espera a que termine la resolución de paquetes y entra en MainMenu > Jugar > Multijugador.
8. Crea dos perfiles de desarrollo: inicia una instancia con `-cierzoProfile host` y otra con `-cierzoProfile client1` (también se admite `CIERZO_PROFILE`).
9. Crea una sala desde host, copia el código y únete desde client1. No introduzcas IP pública ni abras puertos.
10. Para prueba remota genera dos builds Development y sigue `M24_REMOTE_TEST_CHECKLIST.md`.

Si Unity Services falla, usa **Reintentar** o vuelve a Local/Development Direct. No borres PlayerPrefs para cambiar de perfil: cada perfil produce una identidad de desarrollo independiente.
