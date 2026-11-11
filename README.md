# Comercium

Aplicación de escritorio Windows para una tienda familiar. Funciona con WPF, .NET 10 y SQLite local. La primera ejecución aplica las migraciones y solicita crear la cuenta propietaria sobre una base vacía.

Esta entrega incluye acceso, tablero, catálogo, inventario, ventas, despacho en tienda, compras, proveedores y respaldo. El cobro y la entrega son operaciones independientes. La venta registra el pago, descuenta las existencias y crea el pedido en la misma transacción. El despacho exige verificar cada línea antes de registrar la entrega.

## Empezar

- [Instalación y compilación](docs/instalacion.md)
- [Manual básico de usuario](docs/manual.md)
- [Arquitectura y plan completo](docs/arquitectura.md)

## Estado del proyecto

Esta entrega cubre la operación principal de las etapas 1 a 8. El registro opcional de clientes en la venta se completará con el módulo de clientes. Quedan por desarrollar caja y finanzas, reportes y exportación, inteligencia empresarial y planificación estratégica. El respaldo y la distribución como ejecutable autónomo se adelantaron de la etapa de cierre.
"# rsel" 
