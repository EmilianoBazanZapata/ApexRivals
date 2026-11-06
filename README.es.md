[English](README.md) · **Español**

# 🏎️ Apex Rivals — `archive/blocking`

> **Rama archivada.** El prototipo jugable de Apex Rivals entregado en el Primer Parcial, con todo su historial de desarrollo.

Esta fue la rama principal de trabajo mientras se construía y se hacía el blocking de la primera versión jugable, entre el **1 y el 14 de septiembre de 2026**, mediante los **pull requests 55 a 72**. Ya no se desarrolla: el juego terminado está en [`main`](../../tree/main).

## Qué contiene el prototipo

| Área | Prototipo |
| --- | --- |
| Circuito | Un circuito en greybox (blocking) con checkpoints ordenados, línea de largada y llegada, y obstáculos |
| Carrera | Cuenta regresiva, una vuelta, posición en vivo, llegada y resultados |
| IA | Un rival experimental que sigue una línea de carrera |
| Autos | Vanguard y Striker |
| Garage | Selección de auto con vista previa, mejoras de Motor y Manejo, dinero |
| Progresión | Recompensas según la posición de llegada |
| Guardado | Perfil del jugador en JSON versionado, con backup |
| Interfaz | Menú principal, configuración, garage, HUD, pausa y resultados |
| Input | Teclado y gamepad compatible con Xbox |

## Historial

| Pull requests | Hito |
| --- | --- |
| 55 – 57 | Estructura del proyecto, núcleo del runtime y vehículo con ruedas |
| 58 – 60 | Selección de autos, progresión, mejoras, guardado, flujo de escenas, menú principal y configuración |
| 61 – 63 | Piloto de IA, progresión de carreras y arranque de la aplicación |
| 64 – 67 | Autos de producción y experiencia de menú principal y garage |
| 69 – 70 | Presentación de la escena de carrera e integración del arranque |
| 71 – 72 | Cobertura de tests y correcciones del HUD |

La rama también conserva el primer esqueleto del proyecto, solo con código (1 al 5 de septiembre), unido a este historial.

## Cómo abrirlo

Abrí la carpeta `ApexRivals` con Unity **6000.3.5f2** y dale Play a `Assets/ApexRivals/Scenes/Bootstrap.unity`, o abrí `Race.unity` para ver el circuito en greybox.

## Ramas

| Rama | Propósito |
| --- | --- |
| [`main`](../../tree/main) | Versión actual (1.0) |
| [`release/1.0`](../../tree/release/1.0) | Versión 1.0 con todo el historial |
| `archive/blocking` | Esta rama: prototipo del Primer Parcial |

---

**Apex Rivals** · Binary Suns Studio · Emiliano Bazán-Zapata · Motores de Desarrollo I · UTN FRBA
