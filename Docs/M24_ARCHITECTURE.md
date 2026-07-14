# M24 — Arquitectura online

`UnityServicesBootstrap` es el único inicializador persistente. Expone `IPlayerIdentityService`: `UnityAnonymousIdentityService` para UGS y `OfflinePlayerIdentityService` cuando el proyecto no está configurado. La UI nunca llama a Authentication ni Relay directamente.

`MultiplayerSessionCoordinator` es la máquina de estados de flujo. `UnityMultiplayerSessionService` adapta Multiplayer Sessions: la sesión conserva membresía, código, equipos, Ready, héroe y versión; NGO conserva únicamente la simulación autoritativa de partida.

Al crear o unirse a una sala, Multiplayer Services asigna Relay y la configuración se guarda en `DeferredRelayNetworkHandler`. Al entrar a la arena `MobaNetworkMatchBootstrap` consume esa configuración y llama a `UnityTransport.SetRelayServerData`; no se muestra ni almacena una IP pública. Local/Host/Client directo permanecen aislados como herramientas de desarrollo.

El roster admite hasta diez participantes, cinco por Azure y cinco por Ember. `PlayerId` es la identidad estable, `StableSlot` se asigna por equipo, y el host valida equipo, límite, Ready y compatibilidad de build/protocolo antes de cerrar la sala. Al terminar, el host restablece Ready y la sala se vuelve a abrir para una revancha; el transporte NGO se apaga antes de regresar al MainMenu.

Límites actuales: la selección se reutiliza desde el frontend existente y la validación remota real requiere vincular el Cloud Project; no hay matchmaking, host migration ni reconexión avanzada.

## Navegación durante partida activa

`MatchNavigationState` es la fuente de verdad de navegación en arena: expone si la partida sigue activa, si la vista de juego o el menú está visible, el rol online y si hay una desconexión en curso. No infiere estado a partir del nombre de escena, HUD o `NetworkManager`.

`ActiveMatchMenuOverlay` se inserta como hijo del Canvas ya presente en `MobaGreyboxArena`; no crea Canvas, EventSystem, NetworkManager ni carga escenas. El botón `MENÚ` y Escape muestran una capa de menú sobre el mundo sin usar `Time.timeScale`, por lo que el servidor/host y la simulación continúan. Mientras la capa está abierta, los controladores de cámara, órdenes locales, órdenes de red, habilidades, inventario y minimapa quedan tras la puerta `IsGameplayInputAllowed`; al volver no se conserva un objetivo o clic pendiente.

`VOLVER A LA PARTIDA` solo está disponible para una partida viva. Un resultado final invalida ese retorno. La desconexión pide confirmación y se entrega al bootstrap: el cliente abandona la sesión, el host cierra la sala, NGO se apaga y solo entonces se carga el menú principal. El botón de salida del juego pide igualmente confirmación y, en Editor, detiene Play Mode.
