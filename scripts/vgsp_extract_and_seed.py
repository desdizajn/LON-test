#!/usr/bin/env python3
"""
Extract customs reference data from VGSP (local legacy DB) and produce
a SQL file that MERGE-updates LON's global code lists on the VPS.

Run locally (Windows):
    python scripts/vgsp_extract_and_seed.py
Output:
    docs/vgsp_seed.sql

Then on VPS:
    scp docs/vgsp_seed.sql root@vps:/tmp/
    docker exec -i lon-sqlserver /opt/mssql-tools18/bin/sqlcmd \
        -S localhost -U sa -P "$SQL_SA_PASSWORD" -C -d LONDB \
        < /tmp/vgsp_seed.sql

Seeds (idempotent — MERGE / EXISTS guards):
  - TariffCodes                           (from KnigaNai, ~10 k rows)
  - CodeListItems WHERE ListType='Country'(from Drzava, 248 rows; EU flag + preferential group + WTO flag carried in DescriptionMK)
  - CodeListItems WHERE ListType='UoM'    (from EDMER)
  - CustomsProcedures                      (from CarProc — upsert; existing 4200/3151/6121 are preserved)
"""
import sys, io, os, pyodbc, uuid, datetime

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

VGSP = 'DRIVER={ODBC Driver 17 for SQL Server};SERVER=localhost;DATABASE=VGSP;Trusted_Connection=yes;'
OUT_PATH = os.path.join(os.path.dirname(__file__), '..', 'docs', 'vgsp_seed.sql')

def sql_str(v):
    if v is None:
        return 'NULL'
    if isinstance(v, (int, float)):
        return str(v)
    # Escape single quotes and wrap as N'...' so Cyrillic names survive.
    return "N'" + str(v).replace("'", "''") + "'"

def first_chunk(s, n=500):
    if s is None: return None
    s = str(s).replace('\r', ' ').replace('\n', ' ').strip()
    return s[:n] if s else None

def run():
    conn = pyodbc.connect(VGSP)
    cur = conn.cursor()
    now = datetime.datetime.utcnow().strftime('%Y-%m-%dT%H:%M:%S')
    lines = [
        '-- Auto-generated from VGSP on ' + now + 'Z',
        'SET NOCOUNT ON;',
        ''
    ]

    # ---------- TariffCodes ----------
    cur.execute("""
        SELECT TARSIF, TARBR, TAROZ1, TAROZ2, TAROZ3,
               CAST(NAI AS NVARCHAR(1000)) AS NAI, NaiS,
               EDMER, ST, DDV, FI, FU, PV, CAST(Ex AS NVARCHAR(200)) AS Ex
          FROM KnigaNai
         WHERE TARSIF IS NOT NULL AND LEN(TARSIF) = 10
    """)
    tariff_rows = cur.fetchall()
    print(f'TariffCodes from VGSP: {len(tariff_rows)} rows')
    lines.append('-- === TariffCodes (10-char TARSIF) ===')
    for r in tariff_rows:
        tariff = r.TARSIF
        desc = first_chunk(r.NaiS or r.NAI) or tariff
        uom = (r.EDMER or '').strip() or None
        st = r.ST if r.ST is not None else None
        ddv = r.DDV if r.DDV is not None else None
        lines.append(
            f"IF NOT EXISTS (SELECT 1 FROM TariffCodes WHERE TariffNumber={sql_str(tariff)}) "
            f"INSERT INTO TariffCodes (Id, TariffNumber, TARBR, TAROZ1, TAROZ2, TAROZ3, Description, CustomsRate, UnitMeasure, VATRate, FI, FU, PV, Ex, CreatedAt, CreatedBy, IsDeleted) VALUES "
            f"('{uuid.uuid4()}', {sql_str(tariff)}, {sql_str(r.TARBR)}, {sql_str(r.TAROZ1)}, {sql_str(r.TAROZ2)}, {sql_str(r.TAROZ3)}, "
            f"{sql_str(desc)}, {st if st is not None else 'NULL'}, {sql_str(uom)}, {ddv if ddv is not None else 'NULL'}, "
            f"{sql_str(r.FI)}, {sql_str(r.FU)}, {sql_str(r.PV)}, {sql_str(first_chunk(r.Ex, 100))}, "
            f"{sql_str(now)}, 'vgsp-seed', 0);"
        )
    lines.append('')

    # ---------- Countries (CodeListItems ListType=Country) ----------
    cur.execute("SELECT DrzavaS, DrzavaN, EU, Preferencijal, STO FROM Drzava WHERE DrzavaS IS NOT NULL AND LEN(DrzavaS) = 2")
    drzavi = cur.fetchall()
    print(f'Drzava: {len(drzavi)} rows')
    lines.append('-- === CodeListItems ListType=Country ===')
    for i, r in enumerate(drzavi):
        meta = []
        if r.EU: meta.append('EU=yes')
        if r.Preferencijal: meta.append(f'pref={r.Preferencijal}')
        if r.STO: meta.append('WTO=yes')
        meta_s = ' / '.join(meta) if meta else None
        desc = (r.DrzavaN or '').strip()
        lines.append(
            f"IF NOT EXISTS (SELECT 1 FROM CodeListItems WHERE ListType='Country' AND Code={sql_str(r.DrzavaS)}) "
            f"INSERT INTO CodeListItems (Id, ListType, Code, DescriptionMK, DescriptionEN, SortOrder, IsActive, CreatedAt, CreatedBy, IsDeleted) VALUES "
            f"('{uuid.uuid4()}', 'Country', {sql_str(r.DrzavaS)}, {sql_str(desc + (f' [{meta_s}]' if meta_s else ''))}, {sql_str(desc)}, {i}, 1, "
            f"{sql_str(now)}, 'vgsp-seed', 0);"
        )
    lines.append('')

    # ---------- UoM (CodeListItems ListType=UoM) ----------
    cur.execute("SELECT SIFRA, SKRNAZIV, NAZIV FROM EDMER WHERE SIFRA IS NOT NULL")
    edmers = cur.fetchall()
    print(f'EDMER: {len(edmers)} rows')
    lines.append('-- === CodeListItems ListType=UoM ===')
    for i, r in enumerate(edmers):
        lines.append(
            f"IF NOT EXISTS (SELECT 1 FROM CodeListItems WHERE ListType='UoM' AND Code={sql_str(r.SIFRA)}) "
            f"INSERT INTO CodeListItems (Id, ListType, Code, DescriptionMK, DescriptionEN, SortOrder, IsActive, CreatedAt, CreatedBy, IsDeleted) VALUES "
            f"('{uuid.uuid4()}', 'UoM', {sql_str(r.SIFRA)}, {sql_str(r.NAZIV)}, {sql_str(r.SKRNAZIV)}, {i}, 1, {sql_str(now)}, 'vgsp-seed', 0);"
        )
    lines.append('')

    # ---------- CustomsProcedures ----------
    cur.execute("SELECT CarProcS, CAST(CarProcN AS NVARCHAR(300)) AS CarProcN, VidUI FROM CarProc WHERE CarProcS IS NOT NULL")
    procs = cur.fetchall()
    print(f'CarProc: {len(procs)} rows')
    lines.append('-- === CustomsProcedures (upsert by Code) ===')
    for r in procs:
        code = r.CarProcS.strip()
        name = first_chunk(r.CarProcN) or code
        # ProcedureType mapping: 40 = FinalClearance, 42 = InwardProcessing, 51 = InwardProcessing,
        # 31 = Export (after LON), 10 = Export, 21 = TemporaryImport, 00 = LocalPurchase placeholder.
        ptype_map = {
            '10':5,'31':5,'40':4,'42':3,'51':3,'21':2,'00':1
        }
        ptype = ptype_map.get(code, 1)
        requires_lon = 1 if code in ('42','51') else 0
        lines.append(
            f"IF NOT EXISTS (SELECT 1 FROM CustomsProcedures WHERE Code={sql_str(code)}) "
            f"INSERT INTO CustomsProcedures (Id, Code, Name, Type, Description, RequiresGuarantee, GuaranteePercentage, RequiresMRNTracking, AllowsProduction, AllowsExport, IsActive, CreatedAt, CreatedBy, IsDeleted) VALUES "
            f"('{uuid.uuid4()}', {sql_str(code)}, {sql_str(name)}, {ptype}, {sql_str(name)}, "
            f"{requires_lon}, {100.0 if requires_lon else 0.0}, {requires_lon}, "
            f"{1 if code in ('42','51') else 0}, {1 if code in ('10','31') else 0}, 1, "
            f"{sql_str(now)}, 'vgsp-seed', 0);"
        )
    lines.append('')

    lines.append('-- all table inserts are auto-commit; no outer transaction wrapping')

    os.makedirs(os.path.dirname(OUT_PATH), exist_ok=True)
    with open(OUT_PATH, 'w', encoding='utf-8') as f:
        f.write('\n'.join(lines))
    print(f'Wrote {OUT_PATH} ({len(lines)} lines, ~{os.path.getsize(OUT_PATH)/1024:.0f} KB)')

if __name__ == '__main__':
    run()
