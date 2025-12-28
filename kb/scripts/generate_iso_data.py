#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Генерира комплетни ISO стандарди за земји, валути
Извор: ISO 3166-1 alpha-2 (земји) + ISO 4217 (валути)
"""

import json
import os

# ==============================================================================
# ISO 4217 - ВАЛУТИ (најчести 50+)
# ==============================================================================
currencies = [
    {"code": "EUR", "descriptionMK": "Евро", "descriptionEN": "Euro"},
    {"code": "USD", "descriptionMK": "САД долар", "descriptionEN": "US Dollar"},
    {"code": "GBP", "descriptionMK": "Британска фунта", "descriptionEN": "British Pound"},
    {"code": "CHF", "descriptionMK": "Швајцарски франк", "descriptionEN": "Swiss Franc"},
    {"code": "MKD", "descriptionMK": "Македонски денар", "descriptionEN": "Macedonian Denar"},
    {"code": "JPY", "descriptionMK": "Јапонски јен", "descriptionEN": "Japanese Yen"},
    {"code": "CNY", "descriptionMK": "Кинески јуан", "descriptionEN": "Chinese Yuan"},
    {"code": "CAD", "descriptionMK": "Канадски долар", "descriptionEN": "Canadian Dollar"},
    {"code": "AUD", "descriptionMK": "Австралиски долар", "descriptionEN": "Australian Dollar"},
    {"code": "BGN", "descriptionMK": "Бугарски лев", "descriptionEN": "Bulgarian Lev"},
    {"code": "HRK", "descriptionMK": "Хрватска куна", "descriptionEN": "Croatian Kuna"},
    {"code": "CZK", "descriptionMK": "Чешка круна", "descriptionEN": "Czech Koruna"},
    {"code": "DKK", "descriptionMK": "Данска круна", "descriptionEN": "Danish Krone"},
    {"code": "HUF", "descriptionMK": "Унгарска форинта", "descriptionEN": "Hungarian Forint"},
    {"code": "NOK", "descriptionMK": "Норвешка круна", "descriptionEN": "Norwegian Krone"},
    {"code": "PLN", "descriptionMK": "Полска злота", "descriptionEN": "Polish Zloty"},
    {"code": "RON", "descriptionMK": "Нов романски реу", "descriptionEN": "Romanian Leu"},
    {"code": "RUB", "descriptionMK": "Руска рубља", "descriptionEN": "Russian Ruble"},
    {"code": "SEK", "descriptionMK": "Шведска круна", "descriptionEN": "Swedish Krona"},
    {"code": "TRY", "descriptionMK": "Турска лира", "descriptionEN": "Turkish Lira"},
    {"code": "INR", "descriptionMK": "Индиска рупија", "descriptionEN": "Indian Rupee"},
    {"code": "BRL", "descriptionMK": "Бразилски реал", "descriptionEN": "Brazilian Real"},
    {"code": "ZAR", "descriptionMK": "Јужноафрички ранд", "descriptionEN": "South African Rand"},
    {"code": "KRW", "descriptionMK": "Јужнокорејски вон", "descriptionEN": "South Korean Won"},
    {"code": "MXN", "descriptionMK": "Мексиканско песо", "descriptionEN": "Mexican Peso"},
    {"code": "SGD", "descriptionMK": "Сингапурски долар", "descriptionEN": "Singapore Dollar"},
    {"code": "HKD", "descriptionMK": "Хонконшки долар", "descriptionEN": "Hong Kong Dollar"},
    {"code": "NZD", "descriptionMK": "Новозеландски долар", "descriptionEN": "New Zealand Dollar"},
    {"code": "THB", "descriptionMK": "Тајландски бахт", "descriptionEN": "Thai Baht"},
    {"code": "MYR", "descriptionMK": "Малезиски рингит", "descriptionEN": "Malaysian Ringgit"},
    {"code": "IDR", "descriptionMK": "Индонезиска рупија", "descriptionEN": "Indonesian Rupiah"},
    {"code": "PHP", "descriptionMK": "Филипинско песо", "descriptionEN": "Philippine Peso"},
    {"code": "ILS", "descriptionMK": "Израелски шекел", "descriptionEN": "Israeli Shekel"},
    {"code": "AED", "descriptionMK": "Дирхам на Обединети Арапски Емирати", "descriptionEN": "UAE Dirham"},
    {"code": "SAR", "descriptionMK": "Саудиски ријал", "descriptionEN": "Saudi Riyal"},
    {"code": "RSD", "descriptionMK": "Српски динар", "descriptionEN": "Serbian Dinar"},
    {"code": "ALL", "descriptionMK": "Албански лек", "descriptionEN": "Albanian Lek"},
    {"code": "BAM", "descriptionMK": "Конвертибилна марка", "descriptionEN": "Bosnia-Herzegovina Convertible Mark"}
]

# ==============================================================================
# ISO 3166-1 alpha-2 - ЗЕМЈИ (најважни 50+)
# ==============================================================================
key_countries = [
    {"code": "MK", "descriptionMK": "Северна Македонија", "descriptionEN": "North Macedonia"},
    {"code": "AL", "descriptionMK": "Албанија", "descriptionEN": "Albania"},
    {"code": "BG", "descriptionMK": "Бугарија", "descriptionEN": "Bulgaria"},
    {"code": "GR", "descriptionMK": "Грција", "descriptionEN": "Greece"},
    {"code": "RS", "descriptionMK": "Србија", "descriptionEN": "Serbia"},
    {"code": "XK", "descriptionMK": "Косово", "descriptionEN": "Kosovo"},
    {"code": "ME", "descriptionMK": "Црна Гора", "descriptionEN": "Montenegro"},
    {"code": "HR", "descriptionMK": "Хрватска", "descriptionEN": "Croatia"},
    {"code": "SI", "descriptionMK": "Словенија", "descriptionEN": "Slovenia"},
    {"code": "BA", "descriptionMK": "Босна и Херцеговина", "descriptionEN": "Bosnia and Herzegovina"},
    {"code": "TR", "descriptionMK": "Турција", "descriptionEN": "Turkey"},
    {"code": "DE", "descriptionMK": "Германија", "descriptionEN": "Germany"},
    {"code": "IT", "descriptionMK": "Италија", "descriptionEN": "Italy"},
    {"code": "FR", "descriptionMK": "Франција", "descriptionEN": "France"},
    {"code": "GB", "descriptionMK": "Голема Британија", "descriptionEN": "United Kingdom"},
    {"code": "US", "descriptionMK": "Соединети Американски Држави", "descriptionEN": "United States"},
    {"code": "CN", "descriptionMK": "Кина", "descriptionEN": "China"},
    {"code": "RU", "descriptionMK": "Русија", "descriptionEN": "Russia"},
    {"code": "AT", "descriptionMK": "Австрија", "descriptionEN": "Austria"},
    {"code": "CH", "descriptionMK": "Швајцарија", "descriptionEN": "Switzerland"},
    {"code": "NL", "descriptionMK": "Холандија", "descriptionEN": "Netherlands"},
    {"code": "BE", "descriptionMK": "Белгија", "descriptionEN": "Belgium"},
    {"code": "ES", "descriptionMK": "Шпанија", "descriptionEN": "Spain"},
    {"code": "PT", "descriptionMK": "Португалија", "descriptionEN": "Portugal"},
    {"code": "PL", "descriptionMK": "Полска", "descriptionEN": "Poland"},
    {"code": "CZ", "descriptionMK": "Чешка Република", "descriptionEN": "Czech Republic"},
    {"code": "SK", "descriptionMK": "Словачка", "descriptionEN": "Slovakia"},
    {"code": "HU", "descriptionMK": "Унгарија", "descriptionEN": "Hungary"},
    {"code": "RO", "descriptionMK": "Романија", "descriptionEN": "Romania"},
    {"code": "UA", "descriptionMK": "Украина", "descriptionEN": "Ukraine"},
    {"code": "SE", "descriptionMK": "Шведска", "descriptionEN": "Sweden"},
    {"code": "NO", "descriptionMK": "Норвешка", "descriptionEN": "Norway"},
    {"code": "DK", "descriptionMK": "Данска", "descriptionEN": "Denmark"},
    {"code": "FI", "descriptionMK": "Финска", "descriptionEN": "Finland"},
    {"code": "IE", "descriptionMK": "Ирска", "descriptionEN": "Ireland"},
    {"code": "JP", "descriptionMK": "Јапонија", "descriptionEN": "Japan"},
    {"code": "KR", "descriptionMK": "Кореа", "descriptionEN": "South Korea"},
    {"code": "IN", "descriptionMK": "Индија", "descriptionEN": "India"},
    {"code": "AU", "descriptionMK": "Австралија", "descriptionEN": "Australia"},
    {"code": "CA", "descriptionMK": "Канада", "descriptionEN": "Canada"},
    {"code": "BR", "descriptionMK": "Бразил", "descriptionEN": "Brazil"},
    {"code": "MX", "descriptionMK": "Мексико", "descriptionEN": "Mexico"},
    {"code": "AR", "descriptionMK": "Аргентина", "descriptionEN": "Argentina"},
    {"code": "ZA", "descriptionMK": "Јужна Африка", "descriptionEN": "South Africa"},
    {"code": "EG", "descriptionMK": "Египет", "descriptionEN": "Egypt"},
    {"code": "SA", "descriptionMK": "Саудиска Арабија", "descriptionEN": "Saudi Arabia"},
    {"code": "AE", "descriptionMK": "Обединети Арапски Емирати", "descriptionEN": "United Arab Emirates"},
    {"code": "IL", "descriptionMK": "Израел", "descriptionEN": "Israel"},
    {"code": "TH", "descriptionMK": "Таиланд", "descriptionEN": "Thailand"},
    {"code": "MY", "descriptionMK": "Малезија", "descriptionEN": "Malaysia"}
]

def main():
    print("=" * 80)
    print("🌍 ГЕНЕРИРАЊЕ НА ISO СТАНДАРДИ")
    print("=" * 80)
    
    # Додади Box број и sortOrder
    for i, currency in enumerate(currencies):
        currency["boxNumber"] = "22"
        currency["sortOrder"] = i + 1
    
    for i, country in enumerate(key_countries):
        country["boxNumber"] = "15а"
        country["sortOrder"] = i + 1
    
    # Зачувај валути
    with open("kb/processed/currencies_box22_iso.json", 'w', encoding='utf-8') as f:
        json.dump({
            "listType": "Box22_Currency",
            "boxNumber": "22",
            "totalCodes": len(currencies),
            "codes": currencies
        }, f, ensure_ascii=False, indent=2)
    
    # Зачувај земји
    with open("kb/processed/countries_box15a_iso.json", 'w', encoding='utf-8') as f:
        json.dump({
            "listType": "Box15a_CountryCode",
            "boxNumber": "15а",
            "totalCodes": len(key_countries),
            "codes": key_countries
        }, f, ensure_ascii=False, indent=2)
    
    print(f"\n✅ Креирани:")
    print(f"   └─ Валути (Box 22): {len(currencies)} кодови")
    print(f"   └─ Земји (Box 15а): {len(key_countries)} кодови")
    print(f"\n💾 Зачувано во:")
    print(f"   └─ kb/processed/currencies_box22_iso.json")
    print(f"   └─ kb/processed/countries_box15a_iso.json")
    
    print(f"\n📋 Примери валути:")
    for c in currencies[:10]:
        print(f"      {c['code']} - {c['descriptionMK']}")
    
    print(f"\n📋 Примери земји:")
    for c in key_countries[:10]:
        print(f"      {c['code']} - {c['descriptionMK']}")

if __name__ == "__main__":
    main()
