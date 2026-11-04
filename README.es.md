[English](README.md) · **Español**

# 🏎️ Apex Rivals — `release/1.0`

> Versión **1.0** de Apex Rivals con su **historial de desarrollo completo**.

Esta rama tiene exactamente el mismo juego que [`main`](../../tree/main), pero conserva cada paso que llevó hasta él: la demo jugable y los pull requests posteriores, del **PR 73 al PR 101** (septiembre a noviembre de 2026). `main` presenta la misma versión en un único commit limpio.

Sirve para revisar cómo se construyó, integró y corrigió cada sistema. Para el juego en sí (características, controles, arquitectura y cómo ejecutarlo) ver el [README de main](../../tree/main#readme).

## Qué contiene la versión 1.0

- Carrera profesional con nueve objetivos y la **Final de Apex Rivals**.
- Cuatro circuitos: Apex, Dunes, Crossover y **Riverdale** (solo para la final), de tres vueltas cada uno.
- Tres rivales con IA por carrera, con táctica de carrera, derrape y recuperación.
- Garage con 8 autos para comprar y mejoras de Motor y Manejo.
- Nitro cargado por derrape, clima dinámico, línea de conducción dinámica e intro previa a la carrera con Cinemachine.
- Motores sintetizados, lista de música, post-procesado URP y opciones gráficas.
- Monolito modular: un assembly por módulo.
- 799 tests de Edit Mode y 125 de Play Mode.

## Hitos del historial

| Pull requests | Hito |
| --- | --- |
| 73 | Demo jugable integrada en main |
| 74 – 77 | Navegación de la interfaz, estabilidad, recuperación y limpieza de arquitectura |
| 78 – 85 | Rediseño de la interfaz, tres rivales con IA, tres circuitos, bus de audio y rivales que derrapan |
| 86 | Carrera profesional, compra de autos y la final |
| 87 – 93 | Pantallas de carga, intro con Cinemachine, efectos visuales, semáforo, público, niebla y línea de conducción |
| 94 – 99 | Ocho autos, ajuste de IA y manejo, nitro, rediseño de interfaz y audio, pestaña de controles, tres vueltas |
| 100 | Circuito final Riverdale, garage con ProBuilder y lista de música |
| 101 | Arquitectura modular, post-procesado, opciones gráficas y música en pantallas de carga |

## Ramas

| Rama | Propósito |
| --- | --- |
| [`main`](../../tree/main) | Versión actual (1.0) |
| `release/1.0` | Esta rama: versión 1.0 con todo el historial |
| [`archive/blocking`](../../tree/archive/blocking) | Historial del prototipo inicial / MVP |

---

**Apex Rivals** · Binary Suns Studio · Emiliano Bazán-Zapata · Motores de Desarrollo I · UTN FRBA
