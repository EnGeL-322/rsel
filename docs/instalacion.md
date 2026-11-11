# Instalación de Comercium

## Ejecutable para Windows x64

1. Copia `artifacts/Comercium-win-x64/Comercium.exe` a una carpeta donde quieras conservar el programa.
2. Abre `Comercium.exe`. El ejecutable incluye el runtime de .NET 10. Las operaciones de la tienda funcionan sin Internet.
3. Registra la cuenta propietaria con tu nombre, un usuario y una contraseña de al menos 12 caracteres.

La publicación apunta a Windows x64 con compilación 19041 o posterior. En Windows 10 y 11 utiliza una edición vigente según la matriz de Microsoft. [Versiones de Windows admitidas por .NET](https://learn.microsoft.com/en-us/dotnet/core/install/windows#supported-versions)

La aplicación guarda su base `comercium.db` bajo `%LOCALAPPDATA%\Comercium` en el perfil de Windows que la ejecuta. El ejecutable puede moverse sin mover los datos. Para trasladar la tienda a otra computadora, crea un respaldo desde **Mi cuenta**, ejecuta la misma versión o una posterior compatible en el equipo destino, registra una cuenta inicial y restaura el respaldo. Luego ingresa con las credenciales de la cuenta del respaldo.

Para actualizar una instalación anterior, crea un respaldo, cierra Comercium y reemplaza el ejecutable. Al abrir la nueva versión se aplican las migraciones sobre la base del mismo perfil de Windows. El archivo de respaldo previo también puede restaurarse con la nueva versión.

## Compilar desde el código fuente

Con el SDK .NET 10 instalado, ejecuta desde la carpeta raíz del proyecto:

```powershell
dotnet restore Comercium.slnx
dotnet build Comercium.slnx --no-restore
dotnet test Comercium.slnx --no-build
dotnet run --project src/Comercium.App
```

Si estás en el mismo espacio de trabajo donde se generó la entrega, puedes usar `.\.local-sdk\dotnet.exe` en lugar de `dotnet`. La restauración de NuGet requiere acceso a los paquetes o una caché local. Las pruebas usan una base SQLite temporal separada de la tienda.

Para publicar un ejecutable autónomo:

```powershell
dotnet publish src/Comercium.App -c Release -r win-x64 --self-contained `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None -o artifacts/Comercium-win-x64
```

Las migraciones de EF Core están en `src/Comercium.Infrastructure/Persistence/Migrations`. El programa las aplica al abrir la base. Si la apertura falla, el registro técnico queda en `%LOCALAPPDATA%\Comercium\errores.log`.
