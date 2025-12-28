#!/usr/bin/env python3
"""
Script за креирање на LON валидациски правила
Извор: Правилник за пополнување на царинска декларација, Упатство LON
Output: kb/processed/lon_validation_rules.json
"""

import json
from pathlib import Path

def create_lon_validation_rules():
    """Креира валидациски правила за LON процедури"""
    
    output_file = Path(__file__).parent.parent / "processed" / "lon_validation_rules.json"
    output_file.parent.mkdir(exist_ok=True)
    
    rules = [
        # ===== Box 01 - Декларација =====
        {
            "ruleCode": "BOX01_REQUIRED",
            "fieldName": "DeclarationType",
            "ruleType": "Required",
            "validationLogic": "NOT NULL AND LENGTH > 0",
            "errorMessageMK": "Box 01: Вид на декларација е задолжителен",
            "errorMessageEN": "Box 01: Declaration type is required",
            "severity": "Error",
            "referenceDocument": "Правилник, Член 7",
            "procedureCode": None,
            "priority": 10
        },
        
        # ===== Box 33 - Тарифна ознака =====
        {
            "ruleCode": "BOX33_FORMAT",
            "fieldName": "TariffCode",
            "ruleType": "Format",
            "validationLogic": "REGEX: ^\\d{10}$",
            "errorMessageMK": "Box 33: Тарифната ознака мора да биде точно 10 цифри",
            "errorMessageEN": "Box 33: Tariff code must be exactly 10 digits",
            "severity": "Error",
            "referenceDocument": "Правилник, Член 15",
            "procedureCode": None,
            "priority": 15
        },
        {
            "ruleCode": "BOX33_TARIC_EXISTS",
            "fieldName": "TariffCode",
            "ruleType": "CrossTable",
            "validationLogic": "EXISTS IN TariffCodes WHERE TariffNumber = {value} AND IsActive = TRUE",
            "errorMessageMK": "Box 33: Тарифната ознака не постои во TARIC базата",
            "errorMessageEN": "Box 33: Tariff code does not exist in TARIC database",
            "severity": "Error",
            "referenceDocument": "TARIC база",
            "procedureCode": None,
            "priority": 16
        },
        
        # ===== Box 37 - Процедура (LON специфично) =====
        {
            "ruleCode": "BOX37_LON_PROCEDURES",
            "fieldName": "ProcedureCode",
            "ruleType": "ValueList",
            "validationLogic": "IN ('42 00', '51 00', '31 51')",
            "errorMessageMK": "Box 37: За LON се дозволени само процедури 42 00, 51 00 или 31 51",
            "errorMessageEN": "Box 37: Only procedures 42 00, 51 00 or 31 51 are allowed for LON",
            "severity": "Error",
            "referenceDocument": "Упатство LON, стр. 3",
            "procedureCode": None,
            "priority": 20
        },
        {
            "ruleCode": "LON_AUTHORIZATION_REQUIRED",
            "fieldName": "LONAuthorizationId",
            "ruleType": "Required",
            "validationLogic": "NOT NULL WHEN ProcedureCode IN ('42 00', '51 00')",
            "errorMessageMK": "Процедури 42 00 и 51 00 бараат одобрение за увоз за облагородување (N730)",
            "errorMessageEN": "Procedures 42 00 and 51 00 require inward processing authorization (N730)",
            "severity": "Error",
            "referenceDocument": "Упатство LON, точка 12",
            "procedureCode": "42 00|51 00",
            "priority": 21
        },
        {
            "ruleCode": "LON_GUARANTEE_REQUIRED_4200",
            "fieldName": "GuaranteeReference",
            "ruleType": "Required",
            "validationLogic": "NOT NULL WHEN ProcedureCode = '42 00'",
            "errorMessageMK": "Процедура 42 00 бара инструмент за обезбедување (гаранција)",
            "errorMessageEN": "Procedure 42 00 requires security instrument (guarantee)",
            "severity": "Error",
            "referenceDocument": "Упатство LON, точка 12-13",
            "procedureCode": "42 00",
            "priority": 22
        },
        
        # ===== Box 40 - Duty Rate =====
        {
            "ruleCode": "BOX40_MATCH_TARIC",
            "fieldName": "DutyRate",
            "ruleType": "CrossTable",
            "validationLogic": "MATCH TariffCodes.CustomsRate WHERE TariffNumber = {TariffCode}",
            "errorMessageMK": "Box 40: Царинската стапка не одговара на TARIC базата за оваа тарифна ознака",
            "errorMessageEN": "Box 40: Customs rate does not match TARIC database for this tariff code",
            "severity": "Warning",
            "referenceDocument": "TARIC база, Правилник Член 47",
            "procedureCode": None,
            "priority": 40
        },
        {
            "ruleCode": "BOX40_ZERO_FOR_4200",
            "fieldName": "DutyRate",
            "ruleType": "Calculation",
            "validationLogic": "DutyRate = 0 WHEN ProcedureCode = '42 00'",
            "errorMessageMK": "Box 40: За процедура 42 00 (одложено плаќање) царинската стапка мора да биде 0%",
            "errorMessageEN": "Box 40: For procedure 42 00 (suspension system) customs rate must be 0%",
            "severity": "Error",
            "referenceDocument": "Упатство LON, стр. 2",
            "procedureCode": "42 00",
            "priority": 41
        },
        
        # ===== Box 44 - Документи =====
        {
            "ruleCode": "DOC_N730_REQUIRED",
            "fieldName": "Documents",
            "ruleType": "Required",
            "validationLogic": "EXISTS Document WHERE DocumentType = 'N730' AND ProcedureCode IN ('42 00', '51 00')",
            "errorMessageMK": "Документ N730 (Одобрение за LON) е задолжителен за процедури 42 00 и 51 00",
            "errorMessageEN": "Document N730 (LON Authorization) is mandatory for procedures 42 00 and 51 00",
            "severity": "Error",
            "referenceDocument": "Упатство LON, точка 17",
            "procedureCode": "42 00|51 00",
            "priority": 44
        },
        {
            "ruleCode": "DOC_N380_REQUIRED",
            "fieldName": "Documents",
            "ruleType": "Required",
            "validationLogic": "EXISTS Document WHERE DocumentType = 'N380' AND ProcedureCode IN ('42 00', '51 00')",
            "errorMessageMK": "Документ N380 (Проформа фактура) е задолжителен за процедури 42 00 и 51 00",
            "errorMessageEN": "Document N380 (Pro forma invoice) is mandatory for procedures 42 00 and 51 00",
            "severity": "Error",
            "referenceDocument": "Упатство LON",
            "procedureCode": "42 00|51 00",
            "priority": 44
        },
        {
            "ruleCode": "DOC_N785_REQUIRED_REEXPORT",
            "fieldName": "Documents",
            "ruleType": "Required",
            "validationLogic": "EXISTS Document WHERE DocumentType = 'N785' AND ProcedureCode = '31 51'",
            "errorMessageMK": "Документ N785 (Извозна дозвола) е задолжителен за повторен извоз (31 51)",
            "errorMessageEN": "Document N785 (Export licence) is mandatory for re-exportation (31 51)",
            "severity": "Error",
            "referenceDocument": "Повторен извоз - насока, стр. 2",
            "procedureCode": "31 51",
            "priority": 44
        },
        
        # ===== MRN Tracking за повторен извоз =====
        {
            "ruleCode": "MRN_REQUIRED_REEXPORT",
            "fieldName": "PreviousMRN",
            "ruleType": "Required",
            "validationLogic": "NOT NULL WHEN ProcedureCode = '31 51'",
            "errorMessageMK": "За повторен извоз (31 51) е задолжителен MRN на претходна увозна декларација",
            "errorMessageEN": "For re-exportation (31 51) MRN of previous import declaration is required",
            "severity": "Error",
            "referenceDocument": "Повторен извоз - насока",
            "procedureCode": "31 51",
            "priority": 50
        },
        {
            "ruleCode": "MRN_EXISTS_IN_REGISTRY",
            "fieldName": "PreviousMRN",
            "ruleType": "CrossTable",
            "validationLogic": "EXISTS IN MRNRegistry WHERE MRN = {value} AND IsActive = TRUE",
            "errorMessageMK": "MRN на претходна декларација не постои во регистарот",
            "errorMessageEN": "MRN of previous declaration does not exist in registry",
            "severity": "Error",
            "referenceDocument": "Систем за следење MRN",
            "procedureCode": "31 51",
            "priority": 51
        },
        {
            "ruleCode": "MRN_SUFFICIENT_QUANTITY",
            "fieldName": "Quantity",
            "ruleType": "Calculation",
            "validationLogic": "SUM(UsedQuantity) + {Quantity} <= TotalQuantity FROM MRNRegistry WHERE MRN = {PreviousMRN}",
            "errorMessageMK": "Количината надминува достапното количество од претходната декларација",
            "errorMessageEN": "Quantity exceeds available quantity from previous declaration",
            "severity": "Error",
            "referenceDocument": "Систем за следење MRN",
            "procedureCode": "31 51",
            "priority": 52
        },
        
        # ===== Рок за завршување =====
        {
            "ruleCode": "LON_COMPLETION_PERIOD",
            "fieldName": "DueDate",
            "ruleType": "Calculation",
            "validationLogic": "DueDate <= DeclarationDate + LONAuthorization.CompletionPeriodDays WHEN ProcedureCode IN ('42 00', '51 00')",
            "errorMessageMK": "Рокот за завршување надминува период утврден во одобрението",
            "errorMessageEN": "Completion period exceeds period specified in authorization",
            "severity": "Warning",
            "referenceDocument": "Упатство LON, точка 15",
            "procedureCode": "42 00|51 00",
            "priority": 60
        },
        
        # ===== Евиденција =====
        {
            "ruleCode": "LON_INVENTORY_REQUIRED",
            "fieldName": "Documents",
            "ruleType": "Required",
            "validationLogic": "EXISTS Document WHERE DocumentType = 'N954'",
            "errorMessageMK": "Имателот на одобрение е должен да води евиденција за стока (N954)",
            "errorMessageEN": "Authorization holder must maintain inventory records (N954)",
            "severity": "Warning",
            "referenceDocument": "Упатство LON, точка 15",
            "procedureCode": "42 00|51 00",
            "priority": 70
        },
        
        # ===== Yield Rate (принос) =====
        {
            "ruleCode": "LON_YIELD_RATE_CHECK",
            "fieldName": "Quantity",
            "ruleType": "Calculation",
            "validationLogic": "CompensatingQuantity <= ImportQuantity * LONAuthorizationItem.YieldRate * (1 + AllowedWastePercentage)",
            "errorMessageMK": "Количината на компензациски производ надминува дозволен принос",
            "errorMessageEN": "Compensating product quantity exceeds allowed yield rate",
            "severity": "Warning",
            "referenceDocument": "Упатство LON",
            "procedureCode": "31 51",
            "priority": 80
        }
    ]
    
    # Зачувај
    print(f"💾 Зачувување во: {output_file}")
    with open(output_file, 'w', encoding='utf-8') as f:
        json.dump(rules, f, ensure_ascii=False, indent=2)
    
    # Статистика
    print(f"\n✅ Креирани {len(rules)} валидациски правила:")
    
    by_severity = {}
    by_procedure = {}
    for rule in rules:
        # По сериозност
        sev = rule['severity']
        by_severity[sev] = by_severity.get(sev, 0) + 1
        
        # По процедура
        proc = rule.get('procedureCode') or 'Општо'
        by_procedure[proc] = by_procedure.get(proc, 0) + 1
    
    print(f"\n📊 По сериозност:")
    for sev, count in sorted(by_severity.items()):
        print(f"  {sev}: {count}")
    
    print(f"\n📊 По процедура:")
    for proc, count in sorted(by_procedure.items()):
        print(f"  {proc}: {count}")
    
    print(f"\n📦 Фајл: {output_file}")

if __name__ == "__main__":
    create_lon_validation_rules()
