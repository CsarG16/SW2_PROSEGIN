# Verificación de las HU 3.1 y 3.2

Desde la raíz del repositorio:

```powershell
dotnet test Prosegin.Tests/Prosegin.Tests.csproj
node --test Prosegin.Tests/Frontend/cotizacion-productos.test.cjs
```

Las pruebas de .NET verifican las rutas HTTP, protección antiforgery, validación
de entradas, vistas Razor y persistencia de cabecera y detalles en archivos SQLite
aislados. SUNAT usa un servicio ficticio. Estas pruebas no ejecutan los seeders ni
se conectan a la base MySQL configurada para desarrollo.

Las pruebas de JavaScript ejecutan el código de producción con un DOM simulado.
Cubren agregar, acumular cantidades, limpiar, eliminar, cancelar, restaurar por
cliente, rechazar cantidades inválidas, redondear importes y transferir la
selección del catálogo al primer cliente elegido. Tras integrar test, también
comprueban la edición de precios, sus márgenes, su restauración por cliente y el
rechazo de precios inferiores al costo. Las pruebas HTTP verifican además que
la edición de productos del catálogo continúa funcionando.

## Comprobación manual en navegador

Con la aplicación y su base de datos de desarrollo disponibles:

1. Abrir una cotización para un cliente habilitado, agregar 2 unidades de un
   producto y luego agregar otras 3. Debe existir una sola fila con cantidad 5.
2. Intentar agregar cantidades vacías, cero, negativas y decimales. Debe mostrarse
   un mensaje y conservarse la lista anterior. Repetir editando la cantidad de
   una fila: no debe poder grabarse hasta corregirla.
3. Agregar otro producto y eliminar el primero. Comprobar que disminuye el
   contador, se renumeran las filas y se recalculan subtotal, IGV y Total General.
4. Pulsar Limpiar. Comprobar que los campos y productos se vacían, la cantidad
   vuelve a 1 y el contador y total vuelven a cero.
5. Agregar productos, pulsar Atrás y volver a abrir la cotización. La lista debe
   estar vacía. Repetir usando la navegación atrás del navegador.
6. Mantener un borrador para el cliente A y abrir la cotización del cliente B.
   B no debe heredar los productos de A; al recargar A, su borrador se conserva.
7. Desde el catálogo general, agregar productos y pulsar Continuar con cliente.
   Elegir A: su nueva cotización debe recibir esa selección. Abrir B: no debe
   recibirla. Cancelar o grabar A: la selección no debe reaparecer después.
8. Grabar una lista válida. Comprobar el mensaje exacto "Productos guardados
   correctamente" y consultar Cotizaciones y CotizacionDetalles para verificar
   cliente, productos, cantidades e importes. El borrador debe quedar vacío.
9. Comprobar los textos +Agregar, Total General y Opciones, y que solo se puedan
   seleccionar productos activos.

Los ensayos automatizados no sustituyen la inspección visual en navegador ni una
prueba de integración contra la instancia MySQL de desarrollo.
