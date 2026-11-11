# Comercium

Aplicación de escritorio Windows para una tienda familiar. Funciona con WPF, .NET 10 y SQLite local. La primera ejecución aplica las migraciones y solicita crear la cuenta propietaria sobre una base vacía.

La aplicación incluye acceso, tablero, catálogo, inventario, ventas, clientes, despacho en tienda, compras, proveedores, caja, reportes PDF y Excel, análisis comercial, planificación estratégica y respaldos. La venta registra el pago, descuenta las existencias y crea el pedido en la misma transacción. El despacho exige verificar cada línea antes de registrar la entrega.

## Empezar

- [Instalación y compilación](docs/instalacion.md)
- [Manual básico de usuario](docs/manual.md)
- [Arquitectura y plan completo](docs/arquitectura.md)
- [Prompt ejecutado para completar Comercium](docs/prompt-evolucion.md)

## Estado del proyecto

La publicación para Windows x64 está en `artifacts/Comercium-win-x64`. Las migraciones conservan las ventas de la edición anterior y habilitan los módulos nuevos para la cuenta propietaria. Los comprobantes son de uso interno. El asistente responde consultas comerciales con reglas locales sobre la base de la tienda.
"# rsel" 
