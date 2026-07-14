# M24 — Checklist de prueba remota

Prerequisito: UGS vinculado, Authentication y Relay habilitados, y dos builds de la misma versión/protocolo.

## Escenario A — doméstica + hotspot

1. Ejecuta host con `-cierzoProfile host` y crea sala privada.
2. Copia el código, sin compartir IP ni puerto.
3. En una red móvil/hotspot, ejecuta client con `-cierzoProfile client1` y únete por código.
4. Comprueba nombres, equipos, cinco slots y Ready.
5. El host inicia, ambos confirman héroe y llegan a arena.
6. Mueve, ataca, mata un núcleo y verifica retorno a sala.
7. Abandona y crea otra sala. No debe quedar un NetworkManager ni un puerto ocupado.

## Escenario B — dos ubicaciones

Repite los pasos anteriores desde ubicaciones distintas. Comprueba Host Azure/Client Ember y Host Ember/Client Azure. En ambos casos confirma ownership, HUD local, niebla por equipo, marcador, salida del cliente y cierre de sala cuando se pierde el host.
