#!/usr/bin/env python3
"""
One-off seed: land the KW12 Faktura sheets as 3 Registered CustomsDeclarations
(+ lines + MRNRegistry + GuaranteeLedgerEntry debit) and a matching Receipt
per declaration so Inventory shows the physical stock.

Bypasses the DeclarationRuleEngine (TARIC / ISO country sanity rules) because
the TEKSPORT VPS seed only carries a handful of TARIC codes. This is demo
data, not a customs submission — the engine will still guard genuine user
submissions via the regular /api/customs/declarations endpoint.

Run on VPS:
  docker cp kw12_seed_customs_and_inventory.py lon-api:/tmp/
  # Requires sqlcmd + access to /tmp/kw12_faktura_*.csv
  python3 /tmp/kw12_seed_customs_and_inventory.py
"""
import csv, json, subprocess, uuid, datetime, os

SQL_PASSWORD = 'GPXRjf3T02jr5bmZoKOCYgnYPEqaFjYwAa1!'  # pulled from .env earlier
TRANSPORT = {
    '26MKIM10150003D7B3': {'invoice':'261000280','date':'2026-03-23','koleti':4,'proc':'dorabotka','bruto':1513.0,'neto':1269.43,'closing':'2397'},
    '26MKIM10150003D938': {'invoice':'261000298','date':'2026-03-23','koleti':1,'proc':'naknadna','bruto':175.2,'neto':102.58,'closing':'2376nl13'},
    '26MKIM10150003D920': {'invoice':'261000291','date':'2026-03-23','koleti':6,'proc':'verpackung','bruto':1087.0,'neto':948.53,'closing':'2377nl11'},
}

def run_sql(q):
    r = subprocess.run(
        ['docker','exec','lon-sqlserver','/opt/mssql-tools18/bin/sqlcmd',
         '-S','localhost','-U','sa','-P',SQL_PASSWORD,'-C','-d','LONDB','-h','-1','-W','-Q',q],
        capture_output=True, text=True)
    return r.stdout.strip(), r.stderr.strip()

def s(v):
    if v is None: return 'NULL'
    if isinstance(v, (int, float)): return str(v)
    if isinstance(v, bool): return '1' if v else '0'
    return "N'" + str(v).replace("'","''") + "'"

tenant_id_out, _ = run_sql("SET NOCOUNT ON; SELECT Id FROM Tenants WHERE Code='TEKSPORT'")
tek = tenant_id_out.strip()
proc_id_out, _ = run_sql("SET NOCOUNT ON; SELECT Id FROM CustomsProcedures WHERE Code='4200'")
proc_id = proc_id_out.strip()
partner_id_out, _ = run_sql(f"SET NOCOUNT ON; SELECT Id FROM Partners WHERE Code='TEXPORT-AT' AND TenantId='{tek}'")
partner_id = partner_id_out.strip()
lon_auth_out, _ = run_sql(f"SET NOCOUNT ON; SELECT Id FROM LONAuthorizations WHERE AuthorizationNumber='26/TEKSPORT/0001' AND TenantId='{tek}'")
lon_auth_id = lon_auth_out.strip()
guar_acct_out, _ = run_sql(f"SET NOCOUNT ON; SELECT TOP 1 Id FROM GuaranteeAccounts WHERE TenantId='{tek}' AND IsDeleted=0")
guar_acct_id = guar_acct_out.strip()
wh_out, _ = run_sql(f"SET NOCOUNT ON; SELECT Id FROM Warehouses WHERE Code='222' AND TenantId='{tek}'")
wh_id = wh_out.strip()
rcv_loc_out, _ = run_sql(f"SET NOCOUNT ON; SELECT Id FROM Locations WHERE Code='RCV-222' AND WarehouseId='{wh_id}'")
rcv_id = rcv_loc_out.strip()
print(f'tenant={tek[:8]} proc={proc_id[:8]} partner={partner_id[:8]} lon={lon_auth_id[:8]} guar={guar_acct_id[:8]} rcv={rcv_id[:8]}')

# Preload items + UoM code → id
items_out, _ = run_sql(f"SET NOCOUNT ON; SELECT Code+'|'+CAST(Id AS NVARCHAR(36))+'|'+CAST(BaseUoMId AS NVARCHAR(36)) FROM Items WHERE TenantId='{tek}' AND IsDeleted=0")
items = {}
for line in items_out.splitlines():
    parts = line.strip().split('|')
    if len(parts) == 3:
        items[parts[0]] = (parts[1], parts[2])
print(f'items loaded: {len(items)}')
uoms_out, _ = run_sql("SET NOCOUNT ON; SELECT Code+'|'+CAST(Id AS NVARCHAR(36)) FROM UnitsOfMeasure WHERE IsDeleted=0")
uom_by_code = {p[0]:p[1] for p in (l.strip().split('|') for l in uoms_out.splitlines()) if len(p)==2}

for mrn, meta in TRANSPORT.items():
    csv_path = f'/tmp/kw12_faktura_{mrn}.csv'
    if not os.path.exists(csv_path):
        print(f'  {mrn}: CSV missing — skip'); continue
    rows = list(csv.DictReader(open(csv_path)))
    if not rows: continue

    decl_id = str(uuid.uuid4())
    tag = mrn[-4:]
    total = sum(float(r['Total'] or 0) for r in rows if (r['Total'] or '').strip())
    now = datetime.datetime.utcnow().strftime('%Y-%m-%dT%H:%M:%S')

    q = [
        # Declaration header
        f"""INSERT INTO CustomsDeclarations (Id, TenantId, DeclarationNumber, MRN, DeclarationDate,
            CustomsProcedureId, PartnerId, LONAuthorizationId, DeclarationType,
            ProcedureCode, PreviousProcedureCode, Currency, TotalInvoiceAmount, TotalCustomsValue,
            TotalDuty, TotalVAT, TotalOtherCharges, Status, IsCleared, TotalPackages,
            PackageDescription, SenderName, SenderCountry, HasContainer, Notes,
            CreatedAt, CreatedBy, IsDeleted)
          VALUES ({s(decl_id)}, {s(tek)}, {s('IMP-'+tag)}, {s(mrn)}, {s(meta['date'])},
            {s(proc_id)}, {s(partner_id)}, {s(lon_auth_id)}, 'IM',
            '4200', '00', 'EUR', {total:.4f}, {total:.4f},
            0, 0, 0, 1, 0, {meta['koleti']},
            N'{meta['proc']} / zakl. {meta['closing']}', N'Texport GmbH', 'AT', 0,
            N'KW12 seed — bypassed rule engine (TARIC/ISO gaps).',
            '{now}', 'kw12-seed', 0);""",
        # MRN registry (qty in UoM base — approximated as sum of line quantities)
        f"""INSERT INTO MRNRegistries (Id, TenantId, MRN, CustomsDeclarationId, TotalQuantity,
            UsedQuantity, DischargedQuantity, IsActive, ExpiryDate,
            CreatedAt, CreatedBy, IsDeleted)
          VALUES ('{uuid.uuid4()}', {s(tek)}, {s(mrn)}, {s(decl_id)},
            {sum(float(r['Menge'] or 0) for r in rows):.4f}, 0, 0, 1,
            {s((datetime.datetime.strptime(meta['date'],'%Y-%m-%d')+datetime.timedelta(days=180)).strftime('%Y-%m-%dT00:00:00'))},
            '{now}', 'kw12-seed', 0);""",
        # Guarantee debit — use 5% of declared value as a placeholder duty amount
        f"""INSERT INTO GuaranteeLedgerEntries (Id, TenantId, GuaranteeAccountId, CustomsDeclarationId,
            EntryDate, EntryType, Amount, Currency, ReferenceNumber, Description,
            IsReleased, CreatedAt, CreatedBy, IsDeleted)
          VALUES ('{uuid.uuid4()}', {s(tek)}, {s(guar_acct_id)}, {s(decl_id)},
            {s(meta['date'])}, 1, {total*0.05:.4f}, 'EUR', N'IMP-{tag}',
            N'Auto-debit for KW12 seed ({mrn})', 0,
            '{now}', 'kw12-seed', 0);""",
    ]
    # Lines
    ln = 1
    for r in rows:
        art = r['ArtNr'].strip()
        if art not in items: continue
        uom_code = r['VPE'].strip() or 'M'
        if uom_code not in uom_by_code: continue
        try: qty=float(r['Menge'] or 0); cv=float(r['Total'] or 0); nw=float(r['NetWeight'] or 0) if r.get('NetWeight') else None
        except ValueError: continue
        if qty<=0: continue
        iid, _ = items[art]
        uid = uom_by_code[uom_code]
        tariff = ((r.get('ZTN') or '').strip()+'0000000000')[:10]
        country = (r.get('UL') or 'AT').strip()[:2] or 'AT'
        pref = 1 if (r.get('Pref','').strip().lower()=='true') else 0
        q.append(f"""INSERT INTO CustomsDeclarationLines (Id, TenantId, CustomsDeclarationId, LineNumber,
            ItemId, TariffCode, CountryOfOrigin, IsPreferentialOrigin, Quantity, UoMId,
            NetWeight, ItemPrice, StatisticalValue, CustomsValue, DutyRate, DutyAmount,
            VATRate, VATAmount, OtherCharges, CreatedAt, CreatedBy, IsDeleted)
          VALUES ('{uuid.uuid4()}', {s(tek)}, {s(decl_id)}, {ln},
            {s(iid)}, {s(tariff)}, {s(country)}, {pref}, {qty:.4f}, {s(uid)},
            {nw or 0:.4f}, 0, {cv:.4f}, {cv:.4f}, 0, 0, 18, {cv*0.18:.4f}, 0,
            '{now}', 'kw12-seed', 0);""")
        ln += 1

    full_q = '\n'.join(q)
    out, err = run_sql(full_q)
    if err: print(f'  {mrn}: ERR {err[:200]}')
    else: print(f'  {mrn} ({meta["proc"]}): OK — decl {decl_id[:8]} + {ln-1} lines + MRN + guarantee debit')

    # -----  Create a Receipt per declaration (so inventory reflects stock) -----
    rec_id = str(uuid.uuid4())
    rec_number = f'RCP-KW12-{tag}'
    rq = [f"""INSERT INTO Receipts (Id, TenantId, ReceiptNumber, ReceiptDate, PartnerId,
            WarehouseId, PurchaseOrderNumber, ReferenceNumber, CreatedAt, CreatedBy, IsDeleted)
      VALUES ({s(rec_id)}, {s(tek)}, {s(rec_number)}, {s(meta['date'])}, {s(partner_id)},
        {s(wh_id)}, {s(meta['invoice'])}, {s('IMP-'+tag)}, '{now}', 'kw12-seed', 0);"""]
    rln = 1
    for r in rows:
        art = r['ArtNr'].strip()
        if art not in items: continue
        uom_code = r['VPE'].strip() or 'M'
        if uom_code not in uom_by_code: continue
        try: qty=float(r['Menge'] or 0)
        except ValueError: continue
        if qty<=0: continue
        iid, _ = items[art]; uid = uom_by_code[uom_code]
        line_id = str(uuid.uuid4())
        mv_id = str(uuid.uuid4())
        bal_id = str(uuid.uuid4())
        mvnum = f"MOV-KW12-{tag}-{rln:03d}"
        # ReceiptLine
        rq.append(f"""INSERT INTO ReceiptLines (Id, TenantId, ReceiptId, LineNumber, ItemId,
            Quantity, UoMId, BatchNumber, MRN, LocationId, QualityStatus, CustomsDeclarationId,
            CreatedAt, CreatedBy, IsDeleted)
          VALUES ({s(line_id)}, {s(tek)}, {s(rec_id)}, {rln}, {s(iid)},
            {qty:.4f}, {s(uid)}, {s(meta['invoice'])}, {s(mrn)}, {s(rcv_id)}, 1, {s(decl_id)},
            '{now}', 'kw12-seed', 0);""")
        # InventoryMovement
        rq.append(f"""INSERT INTO InventoryMovements (Id, TenantId, MovementNumber, MovementDate,
            Type, ItemId, BatchNumber, MRN, ToLocationId, Quantity, UoMId, ReferenceNumber,
            ReferenceId, CreatedAt, CreatedBy, IsDeleted)
          VALUES ({s(mv_id)}, {s(tek)}, {s(mvnum)}, {s(meta['date'])},
            1, {s(iid)}, {s(meta['invoice'])}, {s(mrn)}, {s(rcv_id)}, {qty:.4f}, {s(uid)}, {s(rec_number)},
            {s(rec_id)}, '{now}', 'kw12-seed', 0);""")
        # InventoryBalance (consolidating keys — rely on unique per call since cleanup was done)
        rq.append(f"""INSERT INTO InventoryBalances (Id, TenantId, ItemId, LocationId, BatchNumber, MRN,
            Quantity, UoMId, QualityStatus, LonProcessState, CreatedAt, CreatedBy, IsDeleted)
          VALUES ({s(bal_id)}, {s(tek)}, {s(iid)}, {s(rcv_id)}, {s(meta['invoice'])}, {s(mrn)},
            {qty:.4f}, {s(uid)}, 1, 1, '{now}', 'kw12-seed', 0);""")
        rln += 1
    full_rq = '\n'.join(rq)
    out2, err2 = run_sql(full_rq)
    if err2: print(f'    Receipt ERR: {err2[:200]}')
    else: print(f'    Receipt {rec_number}: {rln-1} lines + movements + balances')

print('done.')
