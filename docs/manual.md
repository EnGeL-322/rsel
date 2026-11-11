# Manual básico de Comercium

## Preparar la tienda

1. Abre **Productos** y registra un artículo con código único, categoría, unidad, costo y precios. Puedes indicar su ubicación física, adjuntar una foto y definir el stock mínimo.
2. Abre **Inventario** y selecciona el artículo. Registra una **Entrada** con el conteo inicial. Para conciliar con un conteo posterior, usa **Ajuste** e ingresa el saldo físico observado.
3. Consulta **Inicio** para ver existencias bajas y agotadas. En **Inventario** puedes filtrar esas alertas y consultar los movimientos de cada producto.

## Vender y despachar

1. En **Ventas**, busca un artículo, indica la cantidad y selecciona **Mayorista** cuando corresponda. Agrégalo al carrito y revisa las líneas.
2. Elige el método de pago. Para efectivo, escribe el importe recibido o selecciona **Importe exacto**. Registra la venta con **Confirmar venta** después de recibir el pago.
3. En **Despacho en tienda**, selecciona el pedido generado. Puedes imprimir su lista con la ubicación y cantidad de cada artículo.
4. Selecciona **Iniciar preparación**. Confirma la cantidad indicada en cada línea con **Verificar producto**. Tras comprobar todas las líneas, marca el pedido **Listo para recoger**.
5. Al entregar los artículos en el establecimiento, selecciona **Confirmar entrega**. El historial conserva los responsables, fechas y estados del despacho.

En **Ventas → Historial** puedes imprimir el comprobante interno. Para anular una venta antes de la entrega, escribe un motivo y confirma **Anular venta**. La anulación repone las unidades, cancela el pedido y registra la reversión del pago. Si ya habías cobrado, realiza el reintegro correspondiente al cliente antes de registrar la anulación.

## Recibir compras

1. En **Compras y proveedores**, elige un proveedor o guarda uno nuevo. Agrega cada artículo recibido con su cantidad y costo unitario.
2. **Confirmar recepción** aumenta el inventario y actualiza el costo vigente de los artículos recibidos. Las ventas anteriores conservan su costo original.
3. En **Historial y pagos**, selecciona la compra para ver sus artículos. Registra los pagos efectuados con su importe y método. El filtro de pendientes muestra las compras con saldo por pagar.
4. En **Recibir mercadería**, selecciona un producto para comparar el último costo registrado por proveedor.

## Proteger los datos

En **Mi cuenta**, usa **Crear respaldo** y guarda el archivo `.comercium` en una ubicación segura. Contiene la base, las fotos y las credenciales protegidas por hash.

**Restaurar respaldo** sustituye la base activa. Antes de restaurar, Comercium valida el archivo y guarda una copia de los datos actuales. Después, ingresa con la cuenta del respaldo. La ventana de confirmación del resultado indica dónde quedó la copia previa.
