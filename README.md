# Sysbenchmark

C# / .NET 10 konzolos benchmark Windowsra és Linuxra, CPU- és memóriamérésekkel, JSON-exporttal és opcionális MariaDB-mentéssel.

A C# / .NET 10 console benchmark for Windows and Linux, with CPU and memory measurements, JSON export, and optional MariaDB persistence.

[Magyar](#magyar) · [English](#english)

> A dokumentáció a repó `4461050a6afa719a7f2095394c97d03d8a8a3e3b` állapotának forráskód-ellenőrzése alapján készült, 2026-10-03-án. / Documentation based on source inspection of repository revision `4461050a6afa719a7f2095394c97d03d8a8a3e3b`, on 2026-10-03.

## Magyar

### A projektről

A Sysbenchmark saját tanuló- és portfólióprojekt. Célja a teljesítménymérés, a platformfüggő rendszeradatok kiolvasása, az adatbázis-kezelés és a tesztelés gyakorlása.

A jelenlegi alkalmazás kézzel indított, rövid benchmarkokat futtat. A folyamatos rendszerfigyelés és az Angular-felület tervezett fejlesztés; jelenleg nincs webes API vagy távoli mérésindítás. A konzolos felület magyar nyelvű.

### Elkészült funkciók

- Automatikus Windows/Linux platformválasztás.
- Processzormodell, a folyamat számára elérhető logikai processzorok száma, teljes és elérhető fizikai memória megjelenítése.
- Többszálas, `System.Numerics.Vector<float>` alapú CPU-benchmark.
- Szekvenciális memóriaolvasási benchmark.
- CPU-hőmérséklet kiolvasása a CPU-teszt előtt és után, támogatott szenzor esetén.
- CPU- és memóriateszt külön vagy egymás után történő futtatása.
- UTC időbélyeg minden méréshez.
- Az aktuális alkalmazásfutás eredményeinek JSON-exportja.
- Eszközök és mérések tárolása MariaDB-ben, paraméterezett SQL-lel és tranzakcióval.
- Több gép eredményeinek tárolása ugyanabban az adatbázisban.
- Adatbázis-inicializálási és MySQL-mentési hibák kezelése: a program tovább használható, az elkészült mérések exportálhatók.
- Ideiglenes fájlon keresztüli JSON-írás, az exportálási fájlhibák kezelése.
- Docker Compose-konfiguráció MariaDB-hez és phpMyAdminhoz, tartós adatbázis-kötettel és MariaDB healthcheckkel.
- Windowsos szenzordiagnosztika és egy xUnit CPU-benchmark alapteszt.

### Felépítés és technológiák

| Projekt / fájl | Feladat |
| --- | --- |
| `BenchmarkLab.App` | Konzolos menü, mérésindítás, adatbázis-mentés és JSON-export |
| `BenchmarkLab.Core` | CPU- és memóriabenchmark, `BenchmarkResult` adatmodell |
| `BenchmarkLab.Hardware` | Windows/Linux rendszeradatok, hőmérséklet és szenzordiagnosztika |
| `BenchmarkLab.Tests` | xUnit tesztprojekt |
| `SysBench.slnx` | A négy projektet összefogó solution |
| `compose.yaml` | MariaDB és phpMyAdmin szolgáltatások |

Fő függőségek: .NET 10, MySqlConnector 2.6.2, LibreHardwareMonitorLib 0.9.6, xUnit 2.9.3. A Compose `mariadb:11.8` és `phpmyadmin:5.2` image-eket használ.

### Előfeltételek

- Git és [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
- Windows vagy Linux. A más operációs rendszereken történő normál futtatás nem támogatott.
- Adatbázisos használathoz Docker Compose: Windowson például Docker Desktop Linux-konténerekkel, Linuxon Docker Engine és Compose plugin.
- A Docker csak az adatbázis-szerver gépén szükséges. A C#-alkalmazás közvetlenül az operációs rendszeren fut.
- Windowsos CPU-hőmérséklethez [PawnIO](https://github.com/namazso/PawnIO.Setup/releases), támogatott hardver és rendszergazdai futtatás szükséges lehet. A fejlesztés során használt Windowsos gépen ezekkel működött a kiolvasás.

### Letöltés, fordítás és teszt

```bash
git clone https://github.com/BopAdam/Sysbenchmark.git
cd Sysbenchmark
dotnet build SysBench.slnx
dotnet test SysBench.slnx
```

A build visszaállítja a NuGet-függőségeket. A további parancsokat is a repó gyökeréből futtasd.

Meglévő, helyi módosítás nélküli munkapéldány frissítése:

```bash
git status --short
git pull --ff-only
```

### Gyors indítás adatbázis nélkül

Ha nincs beállítva a `SYSBENCHMARK_DB`, a program adatbázis nélkül is elindul:

```bash
dotnet run --project BenchmarkLab.App
```

Ha egy korábbi adatbázis-beállítást az aktuális terminálból el szeretnél távolítani:

PowerShell:

```powershell
Remove-Item Env:SYSBENCHMARK_DB -ErrorAction SilentlyContinue
```

Bash:

```bash
unset SYSBENCHMARK_DB
```

Válaszd az `1` vagy `2` menüpontot, majd a `4`-est az exporthoz. Adatbázis-mentés és JSON-export nélkül a memóriában lévő eredmények kilépéskor elvesznek.

### Dockeres adatbázis első indítása

**1. Ellenőrizd a portbeállítást.** A repóban lévő `compose.yaml` a fejlesztő helyi hálózati címét is tartalmazza:

```yaml
ports:
  - "127.0.0.1:3307:3306"
  - "192.168.0.151:3307:3306"
```

Ha csak egy gépen használod, a `mariadb` szolgáltatás alatt hagyd meg kizárólag ezt:

```yaml
ports:
  - "127.0.0.1:3307:3306"
```

Többgépes használatnál a második címet cseréld a saját adatbázis-szervered helyi IP-címére. A YAML meglévő behúzását őrizd meg. A phpMyAdmin `127.0.0.1:8080:80` beállítása maradhat.

**2. Hozz létre `.env` fájlt a `compose.yaml` mellé:**

```dotenv
MARIADB_ROOT_PASSWORD='REPLACE_WITH_ROOT_PASSWORD'
MARIADB_PASSWORD='REPLACE_WITH_APP_PASSWORD'
```

A helyőrzőket cseréld két különböző saját jelszóra. Az alkalmazás a `MARIADB_PASSWORD` értékét használja, nem a root jelszavát. Az alábbi egyszerű kapcsolati példákhoz használhatsz hosszú, véletlen betű–szám jelszavakat; összetettebb jelszónál figyelj a shell és a kapcsolati karakterlánc idézési szabályaira.

A `.env` szerepel a `.gitignore` fájlban. Ne commitolj valódi jelszót. A meglévő adatbázis jelszavát a `.env` utólagos átírása önmagában nem változtatja meg.

**3. Indítsd el a szolgáltatásokat:**

```bash
docker compose up -d --wait
docker compose ps
```

Az első indítás letölti az image-eket, létrehozza a `sysbenchmark` adatbázist és a `sysbenchmark_app` felhasználót. A MariaDB-nél `healthy` állapotot várunk. A táblákat a C#-alkalmazás hozza létre az első sikeres adatbázis-inicializáláskor.

| Szolgáltatás | Elérés a szervergépről |
| --- | --- |
| MariaDB | `127.0.0.1:3307` |
| phpMyAdmin | <http://localhost:8080> |
| MariaDB a Compose-hálózaton belül | `mariadb:3306` |

### Az alkalmazás csatlakoztatása

**Windows / PowerShell, az adatbázist futtató gépen:**

```powershell
$env:SYSBENCHMARK_DB = 'Server=127.0.0.1;Port=3307;Database=sysbenchmark;User ID=sysbenchmark_app;Password=REPLACE_WITH_APP_PASSWORD'
dotnet run --project BenchmarkLab.App
```

**Linux / Bash, ha ugyanazon a gépen fut az adatbázis:**

```bash
export SYSBENCHMARK_DB='Server=127.0.0.1;Port=3307;Database=sysbenchmark;User ID=sysbenchmark_app;Password=REPLACE_WITH_APP_PASSWORD'
dotnet run --project BenchmarkLab.App
```

Mindkét példában a `.env` `MARIADB_PASSWORD` értékét add meg. Ezek a parancsok az aktuális terminál környezetét állítják be. A C#-alkalmazás nem olvassa be automatikusan a Compose `.env` fájlját.

### Linuxos kliens és Windowsos közös adatbázis

1. Windowson fusson a Docker Desktop és a MariaDB. A gép ne aludjon el.
2. A `compose.yaml` MariaDB-portjai között legyen a Windows aktuális LAN-címe is. Módosítás után futtasd: `docker compose up -d --wait`.
3. A Windows tűzfalán engedélyezd a bejövő TCP `3307` portot a Linuxos gép IP-címéről, a megbízható privát hálózaton. Ehhez nem kell routeres internetes porttovábbítás.
4. Linuxon a `Server` mező a Windows LAN-címe legyen. Példa a fejlesztés során használt címre:

```bash
export SYSBENCHMARK_DB='Server=192.168.0.151;Port=3307;Database=sysbenchmark;User ID=sysbenchmark_app;Password=REPLACE_WITH_APP_PASSWORD'
dotnet run --project BenchmarkLab.App
```

Portellenőrzés Linuxról, ha az `nc` telepítve van:

```bash
nc -vz -w 5 192.168.0.151 3307
```

A példacímeket a saját hálózatodhoz igazítsd. A kliensgépre nem kell Docker vagy adatbázis-szerver. Az IP-címek megváltozhatnak; a routerben beállított DHCP-címfoglalás segíthet állandó címeket használni. A phpMyAdmin jelenleg csak a szervergép böngészőjéből érhető el.

### Menü és diagnosztika

| Menüpont | Működés |
| --- | --- |
| `1` | CPU-benchmark: `Environment.ProcessorCount` párhuzamos munkafeladat, feladatonként 30 000 000 iteráció |
| `2` | Memóriaolvasás: 256 MiB puffer, 10 bejárás |
| `3` | CPU-, majd memóriateszt; jelenleg két külön adatbázissor |
| `4` | Az aktuális futás összes eredményének JSON-exportja |
| `0` | Kilépés |

A benchmarkok a munka végén maguktól befejeződnek; jelenleg nincs külön leállítógomb.

Rendszeradatok ellenőrzése adatbázis-inicializálás nélkül:

```bash
dotnet run --project BenchmarkLab.App -- --system-info
```

Windowsos szenzordiagnosztika, lehetőleg rendszergazdai terminálból:

```powershell
dotnet run --project BenchmarkLab.App -- --sensors
```

Ez kiírja a könyvtár- és PawnIO-verziót, a jogosultsági állapotot, valamint három szenzorkiolvasást végez. Linuxon jelzi, hogy ez a diagnosztikai mód Windowshoz készült.

### Mit jelentenek a mérések?

- **CPU:** eltelt idő és a program saját műveletszámlálása szerinti sebesség. A számláló képlete: munkafeladatok száma × iterációk × `Vector<float>.Count`. A konzol ezt millió művelet/másodpercben (`MOps/s`) mutatja. Ez nem szabványos FLOPS-mérés vagy általános CPU-pontszám.
- **Memória:** a bejárt bájtok mennyisége osztva az eltelt idővel, `GiB/s` egységben. A mező neve történetileg `ThroughputGbPerSec`, de a számítás 1024-es váltószámot használ. A puffer szintén 256 MiB, bár a tesztnévben `MB` szerepel.
- **RAM-kapacitás:** a `TotalMemoryGb` és `AvailableMemoryGb` értékek numerikusan GiB-ban vannak, bár a jelenlegi konzolfelirat `GB`.
- **Hőmérséklet:** két pillanatnyi CPU-leolvasás, nem maximum vagy átlag. A memóriateszt jelenleg nem olvas hőmérsékletet.
- **Időpont:** a benchmark befejezésekor rögzített UTC-idő. A konzol másodpercig jeleníti meg; a JSON nagyobb pontosságot őriz, az adatbázis `DATETIME(6)` mezője mikroszekundum pontosságú, időzóna-információ nélkül tárolja az UTC-t.

A JIT, a build konfigurációja, a háttérfolyamatok, a gyorsítótárak és az energiagazdálkodás befolyásolhatják az eredményeket. Összehasonlításhoz azonos feltételekkel, több ismétléssel érdemes mérni, például Release konfigurációban:

```bash
dotnet run --configuration Release --project BenchmarkLab.App
```

### Hőmérséklet és platformfüggő adatgyűjtés

- Windows: Registry és Windows API-k a rendszeradatokhoz; LibreHardwareMonitor a szenzorokhoz.
- Linux: `/proc/cpuinfo`, `/proc/meminfo`, valamint a hőmérséklethez `/sys/class/hwmon`.
- A hőmérséklet-olvasó `Package`, `Tctl` vagy `Tdie` címkéjű adatot keres. Nem minden hardveren vagy szenzorelrendezéssel talál megfelelő értéket.
- A hiányzó érték a konzolon `nincs adat`, JSON-ban `null`, MariaDB-ben `NULL`. Ez nem 0 °C-ot jelent.
- A hardverrétegben van CPU-terhelés-leolvasás is, de a konzolos alkalmazás még nem végez rendszeres mintavételezést. A Linuxos CPU-időösszegzés további pontosítást igényel az IRQ/softirq/steal mezők kezelésében.

### JSON és adatbázis

A `benchmark_export.json` az aktuális munkakönyvtárba kerül. A 4-es menüpont mindig az aktuális alkalmazásfutás listáját írja ki, felülírva a korábbi exportot; nincs automatikus hozzáfűzés vagy adatbázisba importálás.

Az export szerkezete: `SchemaVersion` (jelenleg 1), `ExportedAtUtc`, `Device`, `Results`. A mérés mezői: `TestName`, `ElapsedMilliseconds`, `OperationsPerSecond`, `ThroughputGbPerSec`, `MeasuredAtUtc`, `CpuTempBeforeC`, `CpuTempAfterC`. A fájl először ideiglenes néven készül el, majd lecseréli a végleges exportot.

| Tábla | Tartalom |
| --- | --- |
| `Devices` | `Id`, gépnév, operációs rendszer, CPU-modell, teljes memória |
| `Measurements` | `Id`, `DeviceId`, tesztnév, UTC-időpont, futási idő, sebességadatok, nullable hőmérsékletek |

Jelenleg a gépnév és az operációs rendszer párosa azonosítja az eszközt. A gép átnevezése új sort eredményezhet; azonos nevű, azonos operációs rendszerű gépek összevonódhatnak. A `Measurements.DeviceId` idegen kulcs a `Devices.Id` mezőre mutat. Eszköz és időpont szerint összetett index segíti a lekérdezést.

A mentés mérésenként, tranzakcióban történik. Az eredmény előbb a memóriába kerül. Sikertelen mentéskor kézzel exportálható; nincs automatikus újraküldési sor. Ha az induláskori inicializálás sikertelen, az alkalmazást újra kell indítani az adatbázis-mentés bekapcsolásához.

### Adatok megtekintése és szolgáltatások kezelése

Nyisd meg a szervergépen: <http://localhost:8080>. Belépés: `sysbenchmark_app`, jelszó: a `.env` alkalmazásjelszava. Válaszd a `sysbenchmark` adatbázist, majd a `Devices` vagy `Measurements` táblát.

SQL-ellenőrzés a phpMyAdmin SQL fülén:

```sql
SELECT * FROM Devices;
SELECT * FROM Measurements ORDER BY Id DESC LIMIT 10;
```

Terminálból:

```bash
docker compose exec mariadb mariadb -u sysbenchmark_app -p sysbenchmark
```

A kliens bekéri a jelszót. Kilépés az SQL-kliensből: `exit;`.

```bash
# Leállítás az adatok megtartásával
docker compose stop

# Meglévő konténerek újraindítása
docker compose start

# Első indítás vagy Compose-módosítás alkalmazása
docker compose up -d --wait

# Állapot és hibanapló
docker compose ps -a
docker compose logs --tail 80 mariadb
```

A `mariadb_data` Docker-kötet tárolja az adatokat, nem egy repóban lévő `.db` fájl. A `docker compose down -v` a projekt kötetét és vele az adatbázis adatait is törölheti; normál leállításhoz a `stop` parancsot használd.

### Tesztelés és jelenlegi korlátok

A repóban jelenleg egy xUnit teszt található: 4 munkafeladattal, feladatonként 100 000 iterációval futtatja a CPU-benchmarkot, és pozitív futási időt, illetve műveleti sebességet vár. A teszt neve még `CpuBenchmark_ShouldRunAndReturnScore`, de a modellben már nincs `Score` mező. Ez valódi számítást végző alapteszt, nem teljes körű lefedettség.

A fejlesztés során kézzel ellenőrzött működések közé tartozik a Windows/Linux adatbázis-kapcsolat, a Windowsos hőmérsékletmentés, illetve a leállított adatbázis melletti mérés és JSON-export. Ezek nem automatikus integrációs tesztek. A README készítésekor forrásellenőrzés történt, új build vagy hardveres teszt nem.

További korlátok: nincs webes felület, autentikációs rendszer, folyamatos mintavétel, távoli vezérlés vagy tartós offline mentési várólista. A szenzorhibák kezelése sem teljes körű. A repóban található exportfájl nem automatikusan frissülő adatforrás. A `.gitignore` jelenleg a `bin/`, `obj/`, `.env` és `sysbenc.txt` elemeket zárja ki, a JSON-exportot nem.

### Tervezett fejlesztések

- Tartós `DeviceUid` eszközazonosító.
- `BenchmarkRuns` a közös tesztfuttatásokhoz.
- Indítás–Leállítás monitorozás `MonitoringSessions` és `MonitoringSamples` táblákkal.
- Részletesebb automatikus tesztek és a kód felelősségeinek további szétválasztása.
- ASP.NET Core API, majd Angular/TypeScript felület mérésekkel és grafikonokkal.

Ezek a fent megjelölt ellenőrzött repóállapotban még nincsenek megvalósítva.

---

## English

### About

Sysbenchmark is a personal learning and portfolio project for practicing performance measurement, platform-specific system information, database persistence, and testing.

The current application runs short, manually started benchmarks. Continuous monitoring and an Angular frontend are planned; there is no web API or remote measurement control yet. Console messages are currently in Hungarian.

### Implemented features

- Automatic Windows/Linux platform selection.
- CPU model, logical processors available to the process, total and available physical memory.
- Multithreaded CPU workload using `System.Numerics.Vector<float>`.
- Sequential memory read benchmark.
- CPU temperature readings before and after the CPU test when a supported sensor is available.
- Separate CPU/memory tests or sequential execution of both.
- UTC timestamps for measurements.
- JSON export of the current application session's results.
- MariaDB persistence with parameterized SQL and transactions; multiple devices can share the database.
- Handling of database initialization and MySQL save errors, allowing completed measurements to remain available for JSON export.
- JSON writing through a temporary file and handling of export file errors.
- Docker Compose setup for MariaDB and phpMyAdmin, with persistent storage and a MariaDB healthcheck.
- Windows sensor diagnostics and one xUnit CPU benchmark smoke test.

### Structure and dependencies

| Project / file | Responsibility |
| --- | --- |
| `BenchmarkLab.App` | Console menu, benchmark execution, database persistence and JSON export |
| `BenchmarkLab.Core` | CPU/memory benchmarks and the `BenchmarkResult` model |
| `BenchmarkLab.Hardware` | Windows/Linux system information, temperature readings and diagnostics |
| `BenchmarkLab.Tests` | xUnit test project |
| `SysBench.slnx` | Solution containing all four projects |
| `compose.yaml` | MariaDB and phpMyAdmin services |

Main dependencies: .NET 10, MySqlConnector 2.6.2, LibreHardwareMonitorLib 0.9.6 and xUnit 2.9.3. Compose uses the `mariadb:11.8` and `phpmyadmin:5.2` images.

### Prerequisites

- Git and the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
- Windows or Linux; normal application execution on other operating systems is unsupported.
- For database usage: Docker with Compose, such as Docker Desktop using Linux containers on Windows, or Docker Engine with the Compose plugin on Linux.
- Docker is needed only on the database host. The C# application runs directly on the operating system.
- Windows temperature access may require [PawnIO](https://github.com/namazso/PawnIO.Setup/releases), supported hardware and an elevated terminal. This was the working configuration on the Windows development machine.

### Clone, build and test

```bash
git clone https://github.com/BopAdam/Sysbenchmark.git
cd Sysbenchmark
dotnet build SysBench.slnx
dotnet test SysBench.slnx
```

The build restores NuGet dependencies. Run the following commands from the repository root as well.

To update an existing working copy with no local changes:

```bash
git status --short
git pull --ff-only
```

### Quick start without a database

When `SYSBENCHMARK_DB` is unset, the application can run without database persistence:

```bash
dotnet run --project BenchmarkLab.App
```

To remove an existing setting from the current terminal:

PowerShell:

```powershell
Remove-Item Env:SYSBENCHMARK_DB -ErrorAction SilentlyContinue
```

Bash:

```bash
unset SYSBENCHMARK_DB
```

Select `1` or `2`, then `4` to export. Results kept only in memory are lost when the application exits unless saved to the database or exported to JSON.

### First database startup with Docker

**1. Review the host IP binding.** The checked-in `compose.yaml` includes a development-machine LAN address:

```yaml
ports:
  - "127.0.0.1:3307:3306"
  - "192.168.0.151:3307:3306"
```

For local-only usage, keep only this mapping under the `mariadb` service:

```yaml
ports:
  - "127.0.0.1:3307:3306"
```

For access from another machine, replace the second address with your database host's actual LAN IP. Preserve the file's existing YAML indentation. The phpMyAdmin mapping can remain `127.0.0.1:8080:80`.

**2. Create a `.env` file next to `compose.yaml`:**

```dotenv
MARIADB_ROOT_PASSWORD='REPLACE_WITH_ROOT_PASSWORD'
MARIADB_PASSWORD='REPLACE_WITH_APP_PASSWORD'
```

Replace the placeholders with two different passwords. The application uses `MARIADB_PASSWORD`, not the root password. Long random alphanumeric passwords work with the simple connection-string examples below; other characters may require additional shell/connection-string quoting.

The `.env` file is ignored by Git. Do not commit real credentials. Editing `.env` after database initialization does not, by itself, change the existing database account passwords.

**3. Start the services:**

```bash
docker compose up -d --wait
docker compose ps
```

The first start downloads the images and creates the `sysbenchmark` database and the `sysbenchmark_app` user. MariaDB should report `healthy`. The C# application creates its tables during the first successful database initialization.

| Service | Address from the database host |
| --- | --- |
| MariaDB | `127.0.0.1:3307` |
| phpMyAdmin | <http://localhost:8080> |
| MariaDB within the Compose network | `mariadb:3306` |

### Connect the application

**Windows / PowerShell on the database host:**

```powershell
$env:SYSBENCHMARK_DB = 'Server=127.0.0.1;Port=3307;Database=sysbenchmark;User ID=sysbenchmark_app;Password=REPLACE_WITH_APP_PASSWORD'
dotnet run --project BenchmarkLab.App
```

**Linux / Bash when the database is on the same machine:**

```bash
export SYSBENCHMARK_DB='Server=127.0.0.1;Port=3307;Database=sysbenchmark;User ID=sysbenchmark_app;Password=REPLACE_WITH_APP_PASSWORD'
dotnet run --project BenchmarkLab.App
```

Use the same password as `MARIADB_PASSWORD` in `.env`. These commands configure the current terminal session. The C# application does not automatically load Compose's `.env` file.

### Linux client with a shared Windows database host

1. Keep Docker Desktop and MariaDB running on Windows. Prevent the host from sleeping.
2. Publish MariaDB on the Windows host's current LAN address in `compose.yaml`. Apply changes with `docker compose up -d --wait`.
3. Allow inbound TCP port `3307` from the Linux client's IP through Windows Firewall on the trusted private network. Internet router port forwarding is not required.
4. On Linux, set `Server` to the Windows host's LAN address. Example using the development setup's address:

```bash
export SYSBENCHMARK_DB='Server=192.168.0.151;Port=3307;Database=sysbenchmark;User ID=sysbenchmark_app;Password=REPLACE_WITH_APP_PASSWORD'
dotnet run --project BenchmarkLab.App
```

To check connectivity from Linux, if `nc` is installed:

```bash
nc -vz -w 5 192.168.0.151 3307
```

Adapt example addresses to your network. The client does not need Docker or its own database server. DHCP reservations can help keep host/client IP addresses stable. phpMyAdmin is currently available only from a browser on the database host.

### Menu and diagnostics

| Option | Action |
| --- | --- |
| `1` | CPU benchmark: `Environment.ProcessorCount` parallel work items, 30,000,000 iterations per item |
| `2` | Sequential read benchmark: 256 MiB buffer, 10 passes |
| `3` | CPU test followed by memory test; currently stored as two separate measurement rows |
| `4` | Export all results from the current application session to JSON |
| `0` | Exit |

Benchmarks finish automatically after their workload completes; there is no separate stop button yet.

Inspect system information without initializing the database:

```bash
dotnet run --project BenchmarkLab.App -- --system-info
```

Run Windows sensor diagnostics, preferably from an elevated terminal:

```powershell
dotnet run --project BenchmarkLab.App -- --sensors
```

This reports library/PawnIO versions, elevation status and three sensor samples. On Linux, this mode reports that it is intended for Windows.

### Measurement meaning and units

- **CPU:** elapsed time and a workload-specific operation rate. The counter is parallel work items × iterations × `Vector<float>.Count`. The console displays millions of operations per second (`MOps/s`). This is not a standardized FLOPS measurement or a general CPU score.
- **Memory:** bytes traversed divided by elapsed time, expressed as `GiB/s`. The historical property name is `ThroughputGbPerSec`, but the calculation uses powers of 1024. The buffer is 256 MiB even though the test name says `MB`.
- **RAM capacity:** `TotalMemoryGb` and `AvailableMemoryGb` contain GiB values, although the console currently labels them `GB`.
- **Temperature:** two CPU snapshots, not peak or average temperature. The memory test currently does not collect temperature readings.
- **Timestamp:** UTC time recorded when the benchmark finishes. The console displays seconds, JSON retains finer precision, and MariaDB stores UTC in a timezone-less `DATETIME(6)` column with microsecond precision.

JIT compilation, build configuration, background processes, caches and power management can affect results. Compare repeated measurements under consistent conditions, for example using Release mode:

```bash
dotnet run --configuration Release --project BenchmarkLab.App
```

### Temperature and platform-specific data

- Windows uses the Registry and Windows APIs for system information, and LibreHardwareMonitor for sensors.
- Linux reads `/proc/cpuinfo`, `/proc/meminfo` and temperature inputs under `/sys/class/hwmon`.
- The temperature reader looks for labels containing `Package`, `Tctl` or `Tdie`. Not every hardware/sensor layout is supported.
- Missing temperature appears as `nincs adat` in the console, `null` in JSON and `NULL` in MariaDB. This does not mean 0 °C.
- CPU usage reading methods exist in the hardware layer, but the console does not yet perform periodic sampling. The Linux CPU time calculation still needs refinement for IRQ/softirq/steal fields.

### JSON and database model

`benchmark_export.json` is written to the current working directory. Option `4` exports the current application's in-memory result list and overwrites the previous export. There is no automatic append or JSON-to-database import.

The export contains `SchemaVersion` (currently 1), `ExportedAtUtc`, `Device` and `Results`. Measurement fields are `TestName`, `ElapsedMilliseconds`, `OperationsPerSecond`, `ThroughputGbPerSec`, `MeasuredAtUtc`, `CpuTempBeforeC` and `CpuTempAfterC`. JSON is written to a temporary file before replacing the final export.

| Table | Contents |
| --- | --- |
| `Devices` | `Id`, machine name, operating system, CPU model and total memory |
| `Measurements` | `Id`, `DeviceId`, test name, UTC timestamp, elapsed time, rates and nullable temperatures |

Devices are currently identified by machine name plus operating system. Renaming a machine can create a new row; machines with the same name and OS can be combined. `Measurements.DeviceId` references `Devices.Id`. A composite index supports queries by device and measurement time.

Each measurement is saved in a transaction after being added to memory. Failed saves can be exported manually; there is no automatic retry queue. If database initialization fails at startup, restart the application to enable database persistence after fixing connectivity.

### Inspect data and manage services

Open <http://localhost:8080> on the database host. Sign in as `sysbenchmark_app` using the application password from `.env`. Select `sysbenchmark`, then browse `Devices` or `Measurements`.

Example queries in phpMyAdmin's SQL tab:

```sql
SELECT * FROM Devices;
SELECT * FROM Measurements ORDER BY Id DESC LIMIT 10;
```

Alternatively, open the SQL client:

```bash
docker compose exec mariadb mariadb -u sysbenchmark_app -p sysbenchmark
```

Enter the password when prompted. Leave the SQL client with `exit;`.

```bash
# Stop without deleting data
docker compose stop

# Restart existing containers
docker compose start

# First startup or apply Compose changes
docker compose up -d --wait

# Inspect status and database logs
docker compose ps -a
docker compose logs --tail 80 mariadb
```

Data lives in the `mariadb_data` Docker volume, not a `.db` file inside the repository. `docker compose down -v` can remove the project's volume and its database contents. Use `stop` for normal shutdown.

### Testing and current limitations

The repository contains one xUnit test: it runs the CPU benchmark with 4 work items and 100,000 iterations per item, then checks for positive elapsed time and operation rate. Its name is still `CpuBenchmark_ShouldRunAndReturnScore`, although the model no longer has a `Score` field. This is a smoke test executing real computation, not comprehensive coverage.

Manual development checks have covered Windows/Linux database access, Windows temperature persistence, and measurement plus JSON export while the database is stopped. These are not automated integration tests. This README was prepared by inspecting source; no new build or hardware tests were run for the document.

There is no web frontend, application authentication system, continuous sampling, remote control or durable offline queue yet. Sensor error handling is not comprehensive. The checked-in JSON export is not a live data source. The current `.gitignore` excludes `bin/`, `obj/`, `.env` and `sysbenc.txt`, but does not exclude JSON exports.

### Roadmap

- Persistent device identity with `DeviceUid`.
- `BenchmarkRuns` to group tests belonging to one execution.
- Start/stop monitoring with `MonitoringSessions` and `MonitoringSamples`.
- Broader automated testing and further separation of responsibilities.
- ASP.NET Core API, followed by an Angular/TypeScript frontend with measurements and charts.

These features are not implemented in the reviewed repository revision.
