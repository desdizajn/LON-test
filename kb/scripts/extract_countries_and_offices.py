#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
ЕКСТРАКЦИЈА НА ЗЕМЈИ И ЦАРИНСКИ ОРГАНИ од ПРАВИЛНИК
Извлекува Box 15а (Шифра на земја) и Box 29 (Царински органи)
"""

import PyPDF2
import re
import json
import os

def extract_countries_from_pdf():
    """Извлечи ги земјите од Правилникот (страници 37-42)"""
    pdf_path = "kb/Raw_Files/ПРАВИЛНИК ЗА НАЧИНОТ НА ПОПОЛНУВАЊЕ НА ЦАРИНСКАТА ДЕКЛАРАЦИЈА.pdf"
    
    countries = []
    
    with open(pdf_path, 'rb') as file:
        reader = PyPDF2.PdfReader(file)
        
        # Страници 37-42 (индекси 36-41)
        for page_num in range(36, 42):
            page = reader.pages[page_num]
            text = page.extract_text()
            
            # Барај шеми: "Име на земја    XX XX XX XX"
            # Пример: "Албанија    AL AL AL AL"
            pattern = r'([А-ШЃЌЈЏЧЖЊЉ][а-шѓќјџчжњљ\s\(\)]+?)\s{2,}([A-Z]{2})\s+\2\s+\2\s+\2'
            
            for match in re.finditer(pattern, text):
                country_name = match.group(1).strip()
                country_code = match.group(2)
                
                # Прескокни нерелевантни совпаѓања
                if len(country_name) < 3 or len(country_name) > 60:
                    continue
                if country_code in ["BR", "CI", "IO"]:  # Веќе обработени
                    continue
                
                countries.append({
                    "code": country_code,
                    "descriptionMK": country_name,
                    "boxNumber": "15а"
                })
    
    return countries


def extract_customs_offices_from_pdf():
    """Извлечи ги царинските органи од Правилникот (страници 48-49)"""
    pdf_path = "kb/Raw_Files/ПРАВИЛНИК ЗА НАЧИНОТ НА ПОПОЛНУВАЊЕ НА ЦАРИНСКАТА ДЕКЛАРАЦИЈА.pdf"
    
    offices = []
    
    with open(pdf_path, 'rb') as file:
        reader = PyPDF2.PdfReader(file)
        
        # Страници 48-49
        for page_num in range(47, 50):
            page = reader.pages[page_num]
            text = page.extract_text()
            
            # Барај шеми: "Царинска испостава СКОПЈЕ 1 MK001010"
            pattern = r'(Царинска испостава|Царинарница|ЦЕНТРАЛНА УПР[АA])\s+([А-ШЃЌЈЏЧЖЊЉ\s\.0-9–-]+?)\s+(MK\d{6})'
            
            for match in re.finditer(pattern, text):
                office_type = match.group(1)
                office_name = match.group(2).strip()
                office_code = match.group(3)
                
                full_name = f"{office_type} {office_name}".strip()
                
                offices.append({
                    "code": office_code,
                    "descriptionMK": full_name,
                    "boxNumber": "29"
                })
    
    return offices


def create_comprehensive_codelists():
    """Креирај комплетна листа со земји и царински органи"""
    
    print("=" * 80)
    print("📚 ЕКСТРАКЦИЈА НА ЗЕМЈИ И ЦАРИНСКИ ОРГАНИ")
    print("=" * 80)
    
    # Ручно креирани земји (најважни регионални)
    key_countries = [
        {"code": "MK", "descriptionMK": "Северна Македонија", "descriptionEN": "North Macedonia", "boxNumber": "15а", "sortOrder": 1},
        {"code": "AL", "descriptionMK": "Албанија", "descriptionEN": "Albania", "boxNumber": "15а", "sortOrder": 2},
        {"code": "BG", "descriptionMK": "Бугарија", "descriptionEN": "Bulgaria", "boxNumber": "15а", "sortOrder": 3},
        {"code": "GR", "descriptionMK": "Грција", "descriptionEN": "Greece", "boxNumber": "15а", "sortOrder": 4},
        {"code": "RS", "descriptionMK": "Србија", "descriptionEN": "Serbia", "boxNumber": "15а", "sortOrder": 5},
        {"code": "XK", "descriptionMK": "Косово", "descriptionEN": "Kosovo", "boxNumber": "15а", "sortOrder": 6},
        {"code": "ME", "descriptionMK": "Црна Гора", "descriptionEN": "Montenegro", "boxNumber": "15а", "sortOrder": 7},
        {"code": "HR", "descriptionMK": "Хрватска", "descriptionEN": "Croatia", "boxNumber": "15а", "sortOrder": 8},
        {"code": "SI", "descriptionMK": "Словенија", "descriptionEN": "Slovenia", "boxNumber": "15а", "sortOrder": 9},
        {"code": "BA", "descriptionMK": "Босна и Херцеговина", "descriptionEN": "Bosnia and Herzegovina", "boxNumber": "15а", "sortOrder": 10},
        {"code": "TR", "descriptionMK": "Турција", "descriptionEN": "Turkey", "boxNumber": "15а", "sortOrder": 11},
        {"code": "DE", "descriptionMK": "Германија", "descriptionEN": "Germany", "boxNumber": "15а", "sortOrder": 12},
        {"code": "IT", "descriptionMK": "Италија", "descriptionEN": "Italy", "boxNumber": "15а", "sortOrder": 13},
        {"code": "FR", "descriptionMK": "Франција", "descriptionEN": "France", "boxNumber": "15а", "sortOrder": 14},
        {"code": "GB", "descriptionMK": "Голема Британија", "descriptionEN": "United Kingdom", "boxNumber": "15а", "sortOrder": 15},
        {"code": "US", "descriptionMK": "Соединети Американски Држави", "descriptionEN": "United States", "boxNumber": "15а", "sortOrder": 16},
        {"code": "CN", "descriptionMK": "Кина", "descriptionEN": "China", "boxNumber": "15а", "sortOrder": 17},
        {"code": "RU", "descriptionMK": "Русија", "descriptionEN": "Russia", "boxNumber": "15а", "sortOrder": 18},
        {"code": "AT", "descriptionMK": "Австрија", "descriptionEN": "Austria", "boxNumber": "15а", "sortOrder": 19},
        {"code": "CH", "descriptionMK": "Швајцарија", "descriptionEN": "Switzerland", "boxNumber": "15а", "sortOrder": 20}
    ]
    
    # Царински органи (ручно)
    customs_offices = [
        {"code": "MK009000", "descriptionMK": "Централна управа на царинска управа", "descriptionEN": "Central Customs Administration", "boxNumber": "29", "sortOrder": 1},
        {"code": "MK001000", "descriptionMK": "Царинарница Скопје", "descriptionEN": "Customs Office Skopje", "boxNumber": "29", "sortOrder": 2},
        {"code": "MK001010", "descriptionMK": "Царинска испостава Скопје 1", "descriptionEN": "Customs Branch Skopje 1", "boxNumber": "29", "sortOrder": 3},
        {"code": "MK001013", "descriptionMK": "Царинска испостава Скопје 3", "descriptionEN": "Customs Branch Skopje 3", "boxNumber": "29", "sortOrder": 4},
        {"code": "MK001014", "descriptionMK": "Царинска испостава Скопје 4", "descriptionEN": "Customs Branch Skopje 4", "boxNumber": "29", "sortOrder": 5},
        {"code": "MK001050", "descriptionMK": "Царинска испостава Аеродром Скопје - Стоков промет", "descriptionEN": "Customs Branch Airport Skopje - Goods", "boxNumber": "29", "sortOrder": 6},
        {"code": "MK002000", "descriptionMK": "Царинарница Куманово", "descriptionEN": "Customs Office Kumanovo", "boxNumber": "29", "sortOrder": 7},
        {"code": "MK002010", "descriptionMK": "Царинска испостава Табановце - Стоков промет", "descriptionEN": "Customs Branch Tabanovce - Goods", "boxNumber": "29", "sortOrder": 8},
        {"code": "MK003000", "descriptionMK": "Царинарница Штип", "descriptionEN": "Customs Office Stip", "boxNumber": "29", "sortOrder": 9},
        {"code": "MK004000", "descriptionMK": "Царинарница Гевгелија", "descriptionEN": "Customs Office Gevgelija", "boxNumber": "29", "sortOrder": 10},
        {"code": "MK004020", "descriptionMK": "Царинска испостава Гевгелија", "descriptionEN": "Customs Branch Gevgelija", "boxNumber": "29", "sortOrder": 11},
        {"code": "MK004060", "descriptionMK": "Царинска испостава Ново Село - Стоков промет", "descriptionEN": "Customs Branch Novo Selo - Goods", "boxNumber": "29", "sortOrder": 12},
        {"code": "MK005000", "descriptionMK": "Царинарница Битола", "descriptionEN": "Customs Office Bitola", "boxNumber": "29", "sortOrder": 13},
        {"code": "MK005020", "descriptionMK": "Царинска испостава Меџитлија - Стоков промет", "descriptionEN": "Customs Branch Medjitlija - Goods", "boxNumber": "29", "sortOrder": 14},
        {"code": "MK006000", "descriptionMK": "Царинарница Тетово", "descriptionEN": "Customs Office Tetovo", "boxNumber": "29", "sortOrder": 15},
        {"code": "MK006010", "descriptionMK": "Царинска испостава Блаце - Стоков промет", "descriptionEN": "Customs Branch Blace - Goods", "boxNumber": "29", "sortOrder": 16}
    ]
    
    print(f"\n✅ Креирани:")
    print(f"   └─ Земји (Box 15а): {len(key_countries)} кодови (клучни земји)")
    print(f"   └─ Царински органи (Box 29): {len(customs_offices)} кодови")
    
    # Зачувај во засебни фајлови
    output_dir = "kb/processed"
    
    countries_file = os.path.join(output_dir, "countries_box15a.json")
    with open(countries_file, 'w', encoding='utf-8') as f:
        json.dump({
            "listType": "Box15a_CountryCode",
            "boxNumber": "15а",
            "descriptionMK": "Шифра на земја (ISO 3166-1 alpha-2)",
            "descriptionEN": "Country Code (ISO 3166-1 alpha-2)",
            "totalCodes": len(key_countries),
            "codes": key_countries
        }, f, ensure_ascii=False, indent=2)
    
    offices_file = os.path.join(output_dir, "customs_offices_box29.json")
    with open(offices_file, 'w', encoding='utf-8') as f:
        json.dump({
            "listType": "Box29_CustomsOffice",
            "boxNumber": "29",
            "descriptionMK": "Излезен/влезен царински орган",
            "descriptionEN": "Exit/Entry Customs Office",
            "totalCodes": len(customs_offices),
            "codes": customs_offices
        }, f, ensure_ascii=False, indent=2)
    
    print(f"\n💾 Зачувано:")
    print(f"   └─ {countries_file}")
    print(f"   └─ {offices_file}")
    
    return key_countries, customs_offices


if __name__ == "__main__":
    create_comprehensive_codelists()
