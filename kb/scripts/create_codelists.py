#!/usr/bin/env python3
"""
Script за креирање на LON-специфични код листи
Извор: Упатство увоз за облагородување, Правилник
Output: kb/processed/lon_codelists.json
"""

import json
from pathlib import Path

def create_lon_codelists():
    """Креира код листи за LON процедури"""
    
    output_file = Path(__file__).parent.parent / "processed" / "lon_codelists.json"
    output_file.parent.mkdir(exist_ok=True)
    
    codelists = {
        # ===== ПРОЦЕДУРНИ КОДОВИ (Box 37) =====
        "ProcedureCode": [
            {
                "code": "40 00",
                "descriptionMK": "Пуштање во слободен промет",
                "descriptionEN": "Release for free circulation",
                "validForLON": False,
                "sortOrder": 1
            },
            {
                "code": "42 00",
                "descriptionMK": "Увоз за облагородување - систем на одложено плаќање",
                "descriptionEN": "Inward processing - suspension system",
                "validForLON": True,
                "requiresAuthorization": True,
                "requiresGuarantee": True,
                "sortOrder": 2
            },
            {
                "code": "51 00",
                "descriptionMK": "Увоз за облагородување - систем на враќање",
                "descriptionEN": "Inward processing - drawback system",
                "validForLON": True,
                "requiresAuthorization": True,
                "requiresGuarantee": False,
                "sortOrder": 3
            },
            {
                "code": "21 00",
                "descriptionMK": "Привремен увоз",
                "descriptionEN": "Temporary admission",
                "validForLON": False,
                "sortOrder": 4
            },
            {
                "code": "10 00",
                "descriptionMK": "Извоз",
                "descriptionEN": "Export",
                "validForLON": False,
                "sortOrder": 5
            },
            {
                "code": "31 51",
                "descriptionMK": "Повторен извоз по облагородување (од 42 00)",
                "descriptionEN": "Re-exportation following inward processing (from 42 00)",
                "validForLON": True,
                "requiresPreviousMRN": True,
                "sortOrder": 6
            }
        ],
        
        # ===== ВИД НА ДОКУМЕНТИ (Box 44) =====
        "DocumentType": [
            {
                "code": "N380",
                "descriptionMK": "Проформа фактура",
                "descriptionEN": "Pro forma invoice",
                "validForLON": True,
                "mandatoryForProcedures": ["42 00", "51 00"],
                "sortOrder": 1
            },
            {
                "code": "N730",
                "descriptionMK": "Одобрение за увоз за облагородување",
                "descriptionEN": "Authorisation for inward processing",
                "validForLON": True,
                "mandatoryForProcedures": ["42 00", "51 00"],
                "sortOrder": 2
            },
            {
                "code": "N703",
                "descriptionMK": "Трговски договор",
                "descriptionEN": "Commercial contract",
                "validForLON": True,
                "mandatoryForProcedures": ["42 00", "51 00"],
                "sortOrder": 3
            },
            {
                "code": "N785",
                "descriptionMK": "Извозна дозвола",
                "descriptionEN": "Export licence",
                "validForLON": True,
                "mandatoryForProcedures": ["31 51"],
                "sortOrder": 4
            },
            {
                "code": "N954",
                "descriptionMK": "Евиденција за стока",
                "descriptionEN": "Inventory record",
                "validForLON": True,
                "sortOrder": 5
            },
            {
                "code": "N235",
                "descriptionMK": "Пакинг листа",
                "descriptionEN": "Packing list",
                "validForLON": True,
                "sortOrder": 6
            },
            {
                "code": "Y921",
                "descriptionMK": "Превозен документ (CMR, коносман, ...)",
                "descriptionEN": "Transport document (CMR, B/L, ...)",
                "validForLON": True,
                "sortOrder": 7
            }
        ],
        
        # ===== НАЧИН НА ТРАНСПОРТ (Box 25) =====
        "TransportMode": [
            {"code": "1", "descriptionMK": "Поморски транспорт", "descriptionEN": "Maritime transport", "sortOrder": 1},
            {"code": "2", "descriptionMK": "Железнички транспорт", "descriptionEN": "Rail transport", "sortOrder": 2},
            {"code": "3", "descriptionMK": "Друмски транспорт", "descriptionEN": "Road transport", "sortOrder": 3},
            {"code": "4", "descriptionMK": "Воздушен транспорт", "descriptionEN": "Air transport", "sortOrder": 4},
            {"code": "5", "descriptionMK": "Пошта", "descriptionEN": "Postal consignment", "sortOrder": 5},
            {"code": "7", "descriptionMK": "Фиксирани транспортни средства", "descriptionEN": "Fixed transport installations", "sortOrder": 6},
            {"code": "8", "descriptionMK": "Внатрешни водни патишта", "descriptionEN": "Inland waterway transport", "sortOrder": 7},
            {"code": "9", "descriptionMK": "Непознато", "descriptionEN": "Not known", "sortOrder": 8}
        ],
        
        # ===== ВИД НА ПАКУВАЊЕ (Box 31) =====
        "PackageType": [
            {"code": "BX", "descriptionMK": "Кутија", "descriptionEN": "Box", "sortOrder": 1},
            {"code": "CT", "descriptionMK": "Картон", "descriptionEN": "Carton", "sortOrder": 2},
            {"code": "PK", "descriptionMK": "Пакет", "descriptionEN": "Package", "sortOrder": 3},
            {"code": "PA", "descriptionMK": "Палета", "descriptionEN": "Pallet", "sortOrder": 4},
            {"code": "DR", "descriptionMK": "Буре", "descriptionEN": "Drum", "sortOrder": 5},
            {"code": "BG", "descriptionMK": "Вреќа", "descriptionEN": "Bag", "sortOrder": 6},
            {"code": "CN", "descriptionMK": "Контејнер", "descriptionEN": "Container", "sortOrder": 7},
            {"code": "NE", "descriptionMK": "Без пакување", "descriptionEN": "Unpacked", "sortOrder": 8}
        ],
        
        # ===== ОПЕРАЦИЈА НА ОБЛАГОРОДУВАЊЕ =====
        "InwardProcessingOperation": [
            {
                "code": "OBR",
                "descriptionMK": "Обработка (механичка, хемиска, монтажа, демонтажа)",
                "descriptionEN": "Processing (mechanical, chemical, assembly, disassembly)",
                "sortOrder": 1
            },
            {
                "code": "PRE",
                "descriptionMK": "Преработка на стока",
                "descriptionEN": "Transformation of goods",
                "sortOrder": 2
            },
            {
                "code": "POP",
                "descriptionMK": "Поправка на стока",
                "descriptionEN": "Repair of goods",
                "sortOrder": 3
            },
            {
                "code": "POM",
                "descriptionMK": "Употреба на помошни средства за производство",
                "descriptionEN": "Use of auxiliary materials for production",
                "sortOrder": 4
            }
        ],
        
        # ===== ЕКОНОМСКИ УСЛОВ (Член 349 УСЦЗ) =====
        "EconomicCondition": [
            {
                "code": "10",
                "descriptionMK": "Условот е исполнет ако компензациските производи се наменети за извоз",
                "descriptionEN": "Condition met if compensating products are intended for export",
                "requiresMinistryNotification": True,
                "sortOrder": 1
            },
            {
                "code": "11",
                "descriptionMK": "Условот е исполнет ако облагородувањето придонесува за зголемување на економската активност",
                "descriptionEN": "Condition met if processing contributes to increased economic activity",
                "requiresMinistryNotification": True,
                "sortOrder": 2
            },
            {
                "code": "12",
                "descriptionMK": "Условот е исполнет ако нема негативно влијание врз суштинските интереси на домашни производители",
                "descriptionEN": "Condition met if no adverse effect on essential interests of domestic producers",
                "requiresMinistryNotification": True,
                "sortOrder": 3
            }
        ],
        
        # ===== СТАТУС НА LON ОДОБРЕНИЕ =====
        "AuthorizationStatus": [
            {"code": "ACTIVE", "descriptionMK": "Активно", "descriptionEN": "Active", "sortOrder": 1},
            {"code": "SUSPENDED", "descriptionMK": "Суспендирано", "descriptionEN": "Suspended", "sortOrder": 2},
            {"code": "REVOKED", "descriptionMK": "Повлечено", "descriptionEN": "Revoked", "sortOrder": 3},
            {"code": "EXPIRED", "descriptionMK": "Истечено", "descriptionEN": "Expired", "sortOrder": 4},
            {"code": "PENDING", "descriptionMK": "Во обработка", "descriptionEN": "Pending", "sortOrder": 5}
        ]
    }
    
    # Зачувај
    print(f"💾 Зачувување во: {output_file}")
    with open(output_file, 'w', encoding='utf-8') as f:
        json.dump(codelists, f, ensure_ascii=False, indent=2)
    
    # Статистика
    total_codes = sum(len(items) for items in codelists.values())
    print(f"\n✅ Креирани {len(codelists)} листи со вкупно {total_codes} кодови:")
    for list_type, items in codelists.items():
        print(f"  📋 {list_type}: {len(items)} кодови")
        # LON специфични
        lon_specific = [item for item in items if item.get('validForLON')]
        if lon_specific:
            print(f"     └─ LON специфични: {len(lon_specific)}")
    
    print(f"\n📦 Фајл: {output_file}")

if __name__ == "__main__":
    create_lon_codelists()
