#!/usr/bin/env python3
"""KW12 seed — writes SQL to a file per MRN then runs sqlcmd -i to avoid argv-length limits."""
import csv, subprocess, uuid, datetime, os, tempfile

SQL_PASSWORD = 'GPXRjf3T02jr5bmZoKOCYgnYPEqaFjYwAa1!'
TRANSPORT = {
    '26MKIM10150003D7B3': {'invoice':'261000280','date':'2026-03-23','koleti':4,'proc':'dorabotka','closing':'2397'},
    '26MKIM10150003D938': {'invoice':'261000298','date':'2026-03-23','koleti':1,'proc':'naknadna','closing':'2376nl13'},
    '26MKIM10150003D920': {'invoice':'261000291','date':'2026-03-23','koleti':6,'proc':'verpackung','closing':'2377nl11'},
}

def run_file(sql_text):
    # Pipe via stdin to avoid file-permission and argv-length issues.
    r = subprocess.run(
        ['docker','exec','-i','lon-sqlserver','/opt/mssql-tools18/bin/sqlcmd',
         '-S','localhost','-U','sa','-P',SQL_PASSWORD,'-C','-d','LONDB'],
        input=sql_text, capture_output=True, text=True, encoding='utf-8')
    return r.stdout, r.stderr

def run_q(q):
    r = subprocess.run(
        ['docker','exec','lon-sqlserver','/opt/mssql-tools18/bin/sqlcmd',
         '-S','localhost','-U','sa','-P',SQL_PASSWORD,'-C','-d','LONDB','-h','-1','-W','-Q',q],
        capture_output=True, text=True)
    return r.stdout.strip(), r.stderr.strip()

def ss(v):
    if v is None: return 'NULL'
    if isinstance(v, (int, float)): return str(v)
    return "N'" + str(v).replace("'", "''") + "'"

tek, _ = run_q("SET NOCOUNT ON; SELECT Id FROM Tenants WHERE Code='TEKSPORT'")
proc_id, _ = run_q("SET NOCOUNT ON; SELECT Id FROM CustomsProcedures WHERE Code='4200'")
partner_id, _ = run_q(f"SET NOCOUNT ON; SELECT Id FROM Partners WHERE Code='TEXPORT-AT' AND TenantId='{tek}'")
lon_auth_id, _ = run_q(f"SET NOCOUNT ON; SELECT Id FROM LONAuthorizations WHERE AuthorizationNumber='26/TEKSPORT/0001' AND TenantId='{tek}'")
guar_acct_id, _ = run_q(f"SET NOCOUNT ON; SELECT TOP 1 Id FROM GuaranteeAccounts WHERE TenantId='{tek}' AND IsDeleted=0")
wh_id, _ = run_q(f"SET NOCOUNT ON; SELECT Id FROM Warehouses WHERE Code='222' AND TenantId='{tek}'")
rcv_id, _ = run_q(f"SET NOCOUNT ON; SELECT Id FROM Locations WHERE Code='RCV-222' AND WarehouseId='{wh_id}'")
print(f'tek={tek[:8]} proc={proc_id[:8]} partner={partner_id[:8]} lon={lon_auth_id[:8]} guar={guar_acct_id[:8]} rcv={rcv_id[:8]}')

items_out, _ = run_q(f"SET NOCOUNT ON; SELECT Code+'|'+CAST(Id AS NVARCHAR(36))+'|'+CAST(BaseUoMId AS NVARCHAR(36)) FROM Items WHERE TenantId='{tek}' AND IsDeleted=0")
items = {}
for ln in items_out.splitlines():
    p = ln.strip().split('|')
    if len(p) == 3:
        items[p[0]] = (p[1], p[2])
uoms_out, _ = run_q("SET NOCOUNT ON; SELECT Code+'|'+CAST(Id AS NVARCHAR(36)) FROM UnitsOfMeasure WHERE IsDeleted=0")
uoms = {p[0]:p[1] for p in (l.strip().split('|') for l in uoms_out.splitlines()) if len(p) == 2}
print(f'items={len(items)} uoms={len(uoms)}')

for mrn, meta in TRANSPORT.items():
    csv_path = f'/tmp/kw12_faktura_{mrn}.csv'
    if not os.path.exists(csv_path):
        print(f'  SKIP {mrn}'); continue
    rows = list(csv.DictReader(open(csv_path)))
    decl_id = str(uuid.uuid4())
    rec_id = str(uuid.uuid4())
    tag = mrn[-4:]
    total = sum(float(r['Total'] or 0) for r in rows if (r['Total'] or '').strip())
    total_qty = sum(float(r['Menge'] or 0) for r in rows if (r['Menge'] or '').strip())
    now = datetime.datetime.utcnow().strftime('%Y-%m-%dT%H:%M:%S')
    expiry = (datetime.datetime.strptime(meta['date'], '%Y-%m-%d') + datetime.timedelta(days=180)).strftime('%Y-%m-%dT00:00:00')

    sql = []
    sql.append(f"INSERT INTO CustomsDeclarations (Id,TenantId,DeclarationNumber,MRN,DeclarationDate,CustomsProcedureId,PartnerId,LONAuthorizationId,DeclarationType,ProcedureCode,PreviousProcedureCode,Currency,TotalInvoiceAmount,TotalCustomsValue,TotalDuty,TotalVAT,TotalOtherCharges,Status,IsCleared,TotalPackages,PackageDescription,SenderName,SenderCountry,HasContainer,Notes,CreatedAt,CreatedBy,IsDeleted) VALUES ({ss(decl_id)},{ss(tek)},{ss('IMP-'+tag)},{ss(mrn)},{ss(meta['date'])},{ss(proc_id)},{ss(partner_id)},{ss(lon_auth_id)},'IM','4200','00','EUR',{total:.4f},{total:.4f},0,0,0,1,0,{meta['koleti']},N'{meta['proc']} / zakl. {meta['closing']}',N'Texport GmbH','AT',0,N'KW12 seed',{ss(now)},'kw12-seed',0);")
    sql.append(f"INSERT INTO MRNRegistries (Id,TenantId,MRN,CustomsDeclarationId,RegistrationDate,TotalQuantity,UsedQuantity,DischargedQuantity,IsActive,ExpiryDate,CreatedAt,CreatedBy,IsDeleted) VALUES ({ss(str(uuid.uuid4()))},{ss(tek)},{ss(mrn)},{ss(decl_id)},{ss(meta['date'])},{total_qty:.4f},0,0,1,{ss(expiry)},{ss(now)},'kw12-seed',0);")
    sql.append(f"INSERT INTO GuaranteeLedgerEntries (Id,TenantId,GuaranteeAccountId,CustomsDeclarationId,EntryDate,EntryType,Amount,Currency,ReferenceType,ReferenceId,MRN,Description,IsReleased,CreatedAt,CreatedBy,IsDeleted) VALUES ({ss(str(uuid.uuid4()))},{ss(tek)},{ss(guar_acct_id)},{ss(decl_id)},{ss(meta['date'])},1,{total*0.05:.4f},'EUR',N'CustomsDeclaration',{ss(decl_id)},{ss(mrn)},N'KW12 seed debit',0,{ss(now)},'kw12-seed',0);")
    sql.append(f"INSERT INTO Receipts (Id,TenantId,ReceiptNumber,ReceiptDate,PartnerId,WarehouseId,PurchaseOrderNumber,ReferenceNumber,CreatedAt,CreatedBy,IsDeleted) VALUES ({ss(rec_id)},{ss(tek)},{ss('RCP-KW12-'+tag)},{ss(meta['date'])},{ss(partner_id)},{ss(wh_id)},{ss(meta['invoice'])},{ss('IMP-'+tag)},{ss(now)},'kw12-seed',0);")

    ln = 1
    for r in rows:
        art = r['ArtNr'].strip()
        if art not in items: continue
        uom_code = r['VPE'].strip() or 'M'
        if uom_code not in uoms: continue
        try:
            qty = float(r['Menge'] or 0)
            cv = float(r['Total'] or 0)
            nw = float(r['NetWeight'] or 0) if r.get('NetWeight') else 0
        except ValueError:
            continue
        if qty <= 0: continue
        iid, _ = items[art]
        uid = uoms[uom_code]
        tariff = ((r.get('ZTN') or '').strip() + '0000000000')[:10]
        country = (r.get('UL') or 'AT').strip()[:2] or 'AT'
        pref = 1 if (r.get('Pref', '').strip().lower() == 'true') else 0
        sql.append(f"INSERT INTO CustomsDeclarationLines (Id,TenantId,CustomsDeclarationId,LineNumber,ItemId,TariffCode,CountryOfOrigin,IsPreferentialOrigin,Quantity,UoMId,NetWeight,ItemPrice,StatisticalValue,CustomsValue,DutyRate,DutyAmount,VATRate,VATAmount,OtherCharges,CreatedAt,CreatedBy,IsDeleted) VALUES ({ss(str(uuid.uuid4()))},{ss(tek)},{ss(decl_id)},{ln},{ss(iid)},{ss(tariff)},{ss(country)},{pref},{qty:.4f},{ss(uid)},{nw:.4f},0,{cv:.4f},{cv:.4f},0,0,18,{cv*0.18:.4f},0,{ss(now)},'kw12-seed',0);")
        sql.append(f"INSERT INTO ReceiptLines (Id,TenantId,ReceiptId,LineNumber,ItemId,Quantity,UoMId,BatchNumber,MRN,LocationId,QualityStatus,CustomsDeclarationId,CreatedAt,CreatedBy,IsDeleted) VALUES ({ss(str(uuid.uuid4()))},{ss(tek)},{ss(rec_id)},{ln},{ss(iid)},{qty:.4f},{ss(uid)},{ss(meta['invoice'])},{ss(mrn)},{ss(rcv_id)},1,{ss(decl_id)},{ss(now)},'kw12-seed',0);")
        sql.append(f"INSERT INTO InventoryMovements (Id,TenantId,MovementNumber,MovementDate,Type,ItemId,BatchNumber,MRN,ToLocationId,Quantity,UoMId,ReferenceNumber,ReferenceId,CreatedAt,CreatedBy,IsDeleted) VALUES ({ss(str(uuid.uuid4()))},{ss(tek)},{ss('MOV-KW12-'+tag+'-'+str(ln).zfill(3))},{ss(meta['date'])},1,{ss(iid)},{ss(meta['invoice'])},{ss(mrn)},{ss(rcv_id)},{qty:.4f},{ss(uid)},{ss('RCP-KW12-'+tag)},{ss(rec_id)},{ss(now)},'kw12-seed',0);")
        sql.append(f"INSERT INTO InventoryBalances (Id,TenantId,ItemId,LocationId,BatchNumber,MRN,Quantity,UoMId,QualityStatus,LonProcessState,CreatedAt,CreatedBy,IsDeleted) VALUES ({ss(str(uuid.uuid4()))},{ss(tek)},{ss(iid)},{ss(rcv_id)},{ss(meta['invoice'])},{ss(mrn)},{qty:.4f},{ss(uid)},1,1,{ss(now)},'kw12-seed',0);")
        ln += 1

    out, err = run_file('SET NOCOUNT ON;\nBEGIN TRAN;\n' + '\n'.join(sql) + '\nCOMMIT;')
    bad = ('Msg' in out) or ('error' in err.lower())
    if bad:
        print(f'  {mrn}: ERR'); print('   out:', out[:400]); print('   err:', err[:200])
    else:
        print(f'  {mrn} ({meta["proc"]}): OK — {ln-1} lines, total {total:.2f} EUR, qty {total_qty:.2f}')

print('done.')
