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

## Clientes y comprobantes

En **Clientes** registra el nombre, la clasificación y, de forma opcional, el documento y el teléfono. Puedes buscar por esos campos y consultar el número de compras confirmadas y el total acumulado. Desactiva un cliente para retirarlo de la lista de nuevas ventas y conservar su historial.

En **Ventas**, selecciona un cliente o **Público general**. Elegir un cliente mayorista selecciona esa tarifa para los artículos que agregues a continuación. En el historial puedes filtrar por fecha, número de venta, cliente o documento y guardar un comprobante interno en PDF.

## Caja y finanzas

1. En **Caja**, escribe el saldo inicial y selecciona **Abrir caja**. Con la sesión abierta, las ventas en efectivo suman el total cobrado después del cambio y los pagos en efectivo a proveedores reducen el saldo. Tarjetas y transferencias quedan en sus respectivos historiales.
2. Registra ingresos, gastos y retiros manuales con su concepto. Consulta el saldo esperado y el historial de movimientos.
3. Al terminar, ingresa el efectivo contado y cierra la caja. La diferencia es el contado menos el esperado. Puedes revisar sesiones anteriores y sus movimientos.

Puedes vender con caja cerrada. Esos cobros se registran en ventas. Para anular un cobro que se asentó en una sesión, abre una caja para registrar el reintegro. Los gastos registrados en caja se deducen del margen bruto en el reporte financiero. Los retiros y pagos a proveedores afectan el efectivo. El costo de cada venta ya refleja la mercadería consumida.

## Reportes y análisis

En **Reportes**, elige una consulta, el período y la categoría, y selecciona **Aplicar**. Puedes usar los atajos de hoy, semana y mes. PDF y Excel guardan el resultado que ves en la tabla. Los reportes de existencias y reposición muestran el stock actual. En ventas históricas, el costo y el nombre del cliente se toman al confirmar. En ventas previas a esta versión, el filtro de categoría usa la categoría actual del producto.

En **Análisis comercial**, compara las ventas con el período inmediatamente anterior de igual duración. Usa los reportes de productos vendidos, rotación, reposición y productos sin ventas. También puedes escribir preguntas como «¿Cuánto vendí hoy?», «¿Qué productos debo comprar?» o «¿Qué productos llevan 30 días sin venderse?». La rotación divide las unidades vendidas entre el promedio de existencias inicial y final.

## Planificación estratégica

En **Identidad y FODA**, guarda la misión, visión, valores y los cuatro componentes del análisis. En **Objetivos y mediciones**, registra un objetivo con indicador, unidad, meta y fecha límite. Selecciona el objetivo para registrar mediciones sucesivas y consultar su avance. Puedes actualizar la estrategia y guardar la evaluación al cerrar el objetivo.
