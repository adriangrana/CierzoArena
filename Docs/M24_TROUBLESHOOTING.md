# M24 — Resolución de problemas

- **Proyecto online necesita configuración**: vincula el Cloud Project y habilita Authentication + Multiplayer Services/Relay siguiendo `M24_UGS_SETUP.md`.
- **Código inválido**: elimina espacios, usa mayúsculas y revisa que sea el código único de la sala, no un token Relay.
- **Sala cerrada o partida comenzada**: el host debe volver a abrir/terminar la partida; no hay reconexión avanzada en M24.
- **Versión incompatible**: ambos jugadores necesitan la misma build y el mismo protocolo M24.
- **No se puede iniciar**: todos deben tener equipo y estado LISTO; solo el host inicia.
- **Puerto 7777 ocupado**: afecta solo al modo Direct Development. Las salas Relay no usan IP pública ni exigen abrir ese puerto.
- **Pérdida de host**: M24 no migra host; la sala se cierra y los clientes vuelven al menú multijugador.

No copies en tickets ni logs acces tokens, connection data de Relay, contraseñas ni PlayerId completos.
