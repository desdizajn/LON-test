#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
КОМПЛЕТНИ ШИФРАРНИЦИ за LON систем
Извор: ПРАВИЛНИК ЗА НАЧИНОТ НА ПОПОЛНУВАЊЕ НА ЦАРИНСКАТА ДЕКЛАРАЦИЈА - Поглавје II Шифри

Генерира сите потребни шифрарници со:
- Box број (број на рубрика)
- Tooltip (контекстуален опис)
- Опис на македонски и англиски
"""

import json
import os
from datetime import datetime

# ==============================================================================
# COMPLETE CODE LISTS FROM PRAVILNIK - POGLAVJE II ŠIFRI
# ==============================================================================

codelists = {
    # =========================================================================
    # BOX 1 - ДЕКЛАРАЦИЈА (Вид)
    # =========================================================================
    "Box01_DeclarationType": [
        {
            "code": "AA",
            "descriptionMK": "Царинска декларација во редовна постапка",
            "descriptionEN": "Regular customs declaration",
            "boxNumber": "1",
            "tooltip": "Член 72 од Законот",
            "sortOrder": 1
        },
        {
            "code": "BB",
            "descriptionMK": "Непотполна декларација",
            "descriptionEN": "Incomplete declaration",
            "boxNumber": "1",
            "tooltip": "Поедноставена постапка - член 88 став (1) точка (а)",
            "sortOrder": 2
        },
        {
            "code": "CC",
            "descriptionMK": "Поедноставена декларација",
            "descriptionEN": "Simplified declaration",
            "boxNumber": "1",
            "tooltip": "Поедноставена постапка - член 88 став (1) точка (б)",
            "sortOrder": 3
        },
        {
            "code": "DD",
            "descriptionMK": "Редовна декларација пред ставање на увид",
            "descriptionEN": "Regular declaration before presentation",
            "boxNumber": "1",
            "tooltip": "Поднесување пред декларантот да ја стави стоката на увид",
            "sortOrder": 4
        },
        {
            "code": "EE",
            "descriptionMK": "Непотполна декларација пред ставање на увид",
            "descriptionEN": "Incomplete declaration before presentation",
            "boxNumber": "1",
            "tooltip": "Поднесување пред декларантот да ја стави стоката на увид",
            "sortOrder": 5
        },
        {
            "code": "FF",
            "descriptionMK": "Поедноставена декларација пред ставање на увид",
            "descriptionEN": "Simplified declaration before presentation",
            "boxNumber": "1",
            "tooltip": "Поднесување пред декларантот да ја стави стоката на увид",
            "sortOrder": 6
        },
        {
            "code": "XX",
            "descriptionMK": "Дополнителна декларација (BB/EE)",
            "descriptionEN": "Supplementary declaration (BB/EE)",
            "boxNumber": "1",
            "tooltip": "Дополнителна декларација во рамки на поедноставена постапка B и E",
            "sortOrder": 7
        },
        {
            "code": "YY",
            "descriptionMK": "Дополнителна декларација (CC/FF)",
            "descriptionEN": "Supplementary declaration (CC/FF)",
            "boxNumber": "1",
            "tooltip": "Дополнителна декларација во рамки на поедноставена постапка C и F",
            "sortOrder": 8
        },
        {
            "code": "ZZ",
            "descriptionMK": "Декларација со запишување во евиденцијата",
            "descriptionEN": "Declaration by entry in records",
            "boxNumber": "1",
            "tooltip": "Поедноставена постапка - член 88 став (1) точка (в)",
            "sortOrder": 9
        },
        {
            "code": "IM",
            "descriptionMK": "Увоз",
            "descriptionEN": "Import",
            "boxNumber": "1",
            "tooltip": "Друга подрубрика - вид на декларација",
            "sortOrder": 10
        },
        {
            "code": "EX",
            "descriptionMK": "Извоз",
            "descriptionEN": "Export",
            "boxNumber": "1",
            "tooltip": "Друга подрубрика - вид на декларација",
            "sortOrder": 11
        },
        {
            "code": "T1",
            "descriptionMK": "Стока со статус T1 - странска стока",
            "descriptionEN": "T1 status goods - non-EU goods",
            "boxNumber": "1",
            "tooltip": "Трета подрубрика - транзит",
            "sortOrder": 12
        },
        {
            "code": "T2",
            "descriptionMK": "Стока со статус T2 - стока на ЕУ",
            "descriptionEN": "T2 status goods - EU goods",
            "boxNumber": "1",
            "tooltip": "Трета подрубрика - транзит",
            "sortOrder": 13
        }
    ],
    
    # =========================================================================
    # BOX 14 - ДЕКЛАРАНТ/ЗАСТАПНИК
    # =========================================================================
    "Box14_DeclarantStatus": [
        {
            "code": "1",
            "descriptionMK": "Декларант",
            "descriptionEN": "Declarant",
            "boxNumber": "14",
            "tooltip": "Лицето кое поднесува декларација",
            "sortOrder": 1
        },
        {
            "code": "2",
            "descriptionMK": "Застапник (директно застапување)",
            "descriptionEN": "Agent (direct representation)",
            "boxNumber": "14",
            "tooltip": "Член 5 став (2) точка а) од Законот",
            "sortOrder": 2
        },
        {
            "code": "3",
            "descriptionMK": "Застапник (индиректно застапување)",
            "descriptionEN": "Agent (indirect representation)",
            "boxNumber": "14",
            "tooltip": "Член 5 став (2) точка б) од Законот",
            "sortOrder": 3
        }
    ],
    
    # =========================================================================
    # BOX 19 - КОНТЕЈНЕР
    # =========================================================================
    "Box19_Container": [
        {
            "code": "0",
            "descriptionMK": "Стока која не е натоварена во контејнер",
            "descriptionEN": "Goods not loaded in container",
            "boxNumber": "19",
            "tooltip": "Без контејнер",
            "sortOrder": 1
        },
        {
            "code": "1",
            "descriptionMK": "Стока која е натоварена во контејнер",
            "descriptionEN": "Goods loaded in container",
            "boxNumber": "19",
            "tooltip": "Со контејнер",
            "sortOrder": 2
        }
    ],
    
    # =========================================================================
    # BOX 20 - УСЛОВИ НА ИСПОРАКА (INCOTERMS)
    # =========================================================================
    "Box20_IncoTerms": [
        {"code": "EXW", "descriptionMK": "Франко фабрика", "descriptionEN": "Ex Works", "boxNumber": "20", "tooltip": "Било кој транспорт - назначено место", "sortOrder": 1},
        {"code": "FCA", "descriptionMK": "Франко превозник", "descriptionEN": "Free Carrier", "boxNumber": "20", "tooltip": "Било кој транспорт - назначено место", "sortOrder": 2},
        {"code": "FAS", "descriptionMK": "Франко покрај бокот на бродот", "descriptionEN": "Free Alongside Ship", "boxNumber": "20", "tooltip": "Морски и речен транспорт - назначено испратно пристаниште", "sortOrder": 3},
        {"code": "FOB", "descriptionMK": "Франко на палубата на бродот", "descriptionEN": "Free On Board", "boxNumber": "20", "tooltip": "Морски и речен транспорт - назначено испратно пристаниште", "sortOrder": 4},
        {"code": "CFR", "descriptionMK": "Трошоци и возарина", "descriptionEN": "Cost and Freight", "boxNumber": "20", "tooltip": "Морски и речен транспорт - назначено одредишно пристаниште", "sortOrder": 5},
        {"code": "CIF", "descriptionMK": "Трошоци, осигурување и возарина", "descriptionEN": "Cost, Insurance and Freight", "boxNumber": "20", "tooltip": "Морски и речен транспорт - назначено одредишно пристаниште", "sortOrder": 6},
        {"code": "CPT", "descriptionMK": "Превозни трошоци платени до", "descriptionEN": "Carriage Paid To", "boxNumber": "20", "tooltip": "Било кој транспорт - назначено одредиште", "sortOrder": 7},
        {"code": "CIP", "descriptionMK": "Превозни трошоци и осигурување платени до", "descriptionEN": "Carriage and Insurance Paid To", "boxNumber": "20", "tooltip": "Било кој транспорт - назначено одредиште", "sortOrder": 8},
        {"code": "DAP", "descriptionMK": "Испорачано на место", "descriptionEN": "Delivered At Place", "boxNumber": "20", "tooltip": "Било кој транспорт - назначено одредиште", "sortOrder": 9},
        {"code": "DPU", "descriptionMK": "Испорачано на место, истоварено", "descriptionEN": "Delivered at Place Unloaded", "boxNumber": "20", "tooltip": "Било кој транспорт - назначено одредиште", "sortOrder": 10},
        {"code": "DDP", "descriptionMK": "Испорачано оцаринето", "descriptionEN": "Delivered Duty Paid", "boxNumber": "20", "tooltip": "Било кој транспорт - назначено одредиште", "sortOrder": 11}
    ],
    
    # =========================================================================
    # BOX 22 - ВАЛУТА ВО ФАКТУРА
    # =========================================================================
    "Box22_Currency": [
        {"code": "EUR", "descriptionMK": "Евро", "descriptionEN": "Euro", "boxNumber": "22", "sortOrder": 1},
        {"code": "USD", "descriptionMK": "САД долар", "descriptionEN": "US Dollar", "boxNumber": "22", "sortOrder": 2},
        {"code": "GBP", "descriptionMK": "Британска фунта", "descriptionEN": "British Pound", "boxNumber": "22", "sortOrder": 3},
        {"code": "CHF", "descriptionMK": "Швајцарски франк", "descriptionEN": "Swiss Franc", "boxNumber": "22", "sortOrder": 4},
        {"code": "MKD", "descriptionMK": "Македонски денар", "descriptionEN": "Macedonian Denar", "boxNumber": "22", "sortOrder": 5},
        {"code": "BGN", "descriptionMK": "Бугарски лев", "descriptionEN": "Bulgarian Lev", "boxNumber": "22", "sortOrder": 6},
        {"code": "HRK", "descriptionMK": "Хрватска куна", "descriptionEN": "Croatian Kuna", "boxNumber": "22", "sortOrder": 7},
        {"code": "TRY", "descriptionMK": "Турска лира", "descriptionEN": "Turkish Lira", "boxNumber": "22", "sortOrder": 8},
        {"code": "RUB", "descriptionMK": "Руска рубља", "descriptionEN": "Russian Ruble", "boxNumber": "22", "sortOrder": 9},
        {"code": "CNY", "descriptionMK": "Кинески јуан", "descriptionEN": "Chinese Yuan", "boxNumber": "22", "sortOrder": 10},
        {"code": "JPY", "descriptionMK": "Јапонски јен", "descriptionEN": "Japanese Yen", "boxNumber": "22", "sortOrder": 11},
        {"code": "CAD", "descriptionMK": "Канадски долар", "descriptionEN": "Canadian Dollar", "boxNumber": "22", "sortOrder": 12},
        {"code": "AUD", "descriptionMK": "Австралиски долар", "descriptionEN": "Australian Dollar", "boxNumber": "22", "sortOrder": 13},
        {"code": "SEK", "descriptionMK": "Шведска круна", "descriptionEN": "Swedish Krona", "boxNumber": "22", "sortOrder": 14},
        {"code": "NOK", "descriptionMK": "Норвешка круна", "descriptionEN": "Norwegian Krone", "boxNumber": "22", "sortOrder": 15},
        {"code": "DKK", "descriptionMK": "Данска круна", "descriptionEN": "Danish Krone", "boxNumber": "22", "sortOrder": 16},
        {"code": "CZK", "descriptionMK": "Чешка круна", "descriptionEN": "Czech Koruna", "boxNumber": "22", "sortOrder": 17},
        {"code": "HUF", "descriptionMK": "Унгарска форинта", "descriptionEN": "Hungarian Forint", "boxNumber": "22", "sortOrder": 18},
        {"code": "PLN", "descriptionMK": "Полска злота", "descriptionEN": "Polish Zloty", "boxNumber": "22", "sortOrder": 19},
        {"code": "RON", "descriptionMK": "Нов романски реу", "descriptionEN": "Romanian Leu", "boxNumber": "22", "sortOrder": 20}
    ],
    
    # =========================================================================
    # BOX 24 - ПРИРОДА НА ТРАНСАКЦИЈАТА (двоцифрен код)
    # =========================================================================
    "Box24_NatureOfTransaction": [
        {"code": "11", "descriptionMK": "Конечно купување/продажба", "descriptionEN": "Outright purchase/sale", "boxNumber": "24", "tooltip": "Трансакција со промена на сопственост со плаќање", "sortOrder": 1},
        {"code": "12", "descriptionMK": "Набавка за продажба на пробен период или консигнација", "descriptionEN": "Supply for sale on approval or consignment", "boxNumber": "24", "tooltip": "Трансакција со промена на сопственост со плаќање", "sortOrder": 2},
        {"code": "13", "descriptionMK": "Бартер трговија (компензација)", "descriptionEN": "Barter trade", "boxNumber": "24", "tooltip": "Трансакција со промена на сопственост со плаќање", "sortOrder": 3},
        {"code": "14", "descriptionMK": "Финансиски лизинг (изнајмување на отплата)", "descriptionEN": "Financial leasing", "boxNumber": "24", "tooltip": "Трансакција со промена на сопственост со плаќање", "sortOrder": 4},
        {"code": "19", "descriptionMK": "Друго", "descriptionEN": "Other", "boxNumber": "24", "tooltip": "Трансакција со промена на сопственост - друго", "sortOrder": 5},
        {"code": "21", "descriptionMK": "Враќање на стока", "descriptionEN": "Return of goods", "boxNumber": "24", "tooltip": "Враќање и замена без надоместок", "sortOrder": 6},
        {"code": "22", "descriptionMK": "Замена на вратена стока", "descriptionEN": "Replacement for returned goods", "boxNumber": "24", "tooltip": "Враќање и замена без надоместок", "sortOrder": 7},
        {"code": "23", "descriptionMK": "Замена (гаранција) на стока која не е вратена", "descriptionEN": "Replacement (warranty) of goods not returned", "boxNumber": "24", "tooltip": "Враќање и замена без надоместок", "sortOrder": 8},
        {"code": "29", "descriptionMK": "Друго", "descriptionEN": "Other", "boxNumber": "24", "tooltip": "Враќање и замена - друго", "sortOrder": 9},
        {"code": "30", "descriptionMK": "Трансакција без финансиски надоместок (хуманитарни пратки)", "descriptionEN": "Transaction without financial compensation", "boxNumber": "24", "tooltip": "Промена на сопственост без надоместок", "sortOrder": 10},
        {"code": "41", "descriptionMK": "Стока за облагородување (ќе се врати)", "descriptionEN": "Goods for processing (to be returned)", "boxNumber": "24", "tooltip": "Постапки со цел облагородување - со враќање", "sortOrder": 11},
        {"code": "42", "descriptionMK": "Стока за облагородување (нема да се врати)", "descriptionEN": "Goods for processing (not to be returned)", "boxNumber": "24", "tooltip": "Постапки со цел облагородување - без враќање", "sortOrder": 12},
        {"code": "51", "descriptionMK": "Стока после облагородување (се враќа)", "descriptionEN": "Goods after processing (returning)", "boxNumber": "24", "tooltip": "Постапки после облагородување - враќање", "sortOrder": 13},
        {"code": "52", "descriptionMK": "Стока после облагородување (не се враќа)", "descriptionEN": "Goods after processing (not returning)", "boxNumber": "24", "tooltip": "Постапки после облагородување - без враќање", "sortOrder": 14},
        {"code": "61", "descriptionMK": "Привремен увоз/извоз заради закуп (оперативен лизинг < 2 години)", "descriptionEN": "Temporary import/export for hire/lease (< 2 years)", "boxNumber": "24", "tooltip": "Специфични трансакции", "sortOrder": 15},
        {"code": "62", "descriptionMK": "Друг привремен увоз/извоз (< 2 години)", "descriptionEN": "Other temporary import/export (< 2 years)", "boxNumber": "24", "tooltip": "Специфични трансакции", "sortOrder": 16},
        {"code": "63", "descriptionMK": "Странско вложување", "descriptionEN": "Foreign investment", "boxNumber": "24", "tooltip": "Специфични трансакции", "sortOrder": 17},
        {"code": "64", "descriptionMK": "Поправка и одржување со плаќање", "descriptionEN": "Repair and maintenance with payment", "boxNumber": "24", "tooltip": "Специфични трансакции", "sortOrder": 18},
        {"code": "65", "descriptionMK": "Работи кои следат после поправка и одржување со плаќање", "descriptionEN": "Goods following repair/maintenance with payment", "boxNumber": "24", "tooltip": "Специфични трансакции", "sortOrder": 19},
        {"code": "66", "descriptionMK": "Бесплатна поправка и одржување", "descriptionEN": "Free repair and maintenance", "boxNumber": "24", "tooltip": "Специфични трансакции", "sortOrder": 20},
        {"code": "67", "descriptionMK": "Работи кои следат после бесплатна поправка и одржување", "descriptionEN": "Goods following free repair/maintenance", "boxNumber": "24", "tooltip": "Специфични трансакции", "sortOrder": 21},
        {"code": "68", "descriptionMK": "Враќање во непроменета состојба на несоодветни стоки", "descriptionEN": "Return in unchanged state of unsuitable goods", "boxNumber": "24", "tooltip": "Специфични трансакции", "sortOrder": 22},
        {"code": "70", "descriptionMK": "Трансакција во врска со заеднички одбранбени/производни програми", "descriptionEN": "Transaction related to joint defense/production programs", "boxNumber": "24", "tooltip": "Меѓувладини програми", "sortOrder": 23},
        {"code": "80", "descriptionMK": "Набавка на градежен материјал во рамки на генерален договор", "descriptionEN": "Supply of construction materials under general contract", "boxNumber": "24", "tooltip": "Генерални договори за изградба", "sortOrder": 24},
        {"code": "91", "descriptionMK": "Изнајмување, позајмување и оперативен лизинг (> 24 месеци)", "descriptionEN": "Hire, loan and operational leasing (> 24 months)", "boxNumber": "24", "tooltip": "Други трансакции", "sortOrder": 25},
        {"code": "99", "descriptionMK": "Друго", "descriptionEN": "Other", "boxNumber": "24", "tooltip": "Други трансакции", "sortOrder": 26}
    ],
    
    # =========================================================================
    # BOX 25 - ВИД НА ТРАНСПОРТ НА ГРАНИЦА
    # =========================================================================
    "Box25_TransportMode": [
        {"code": "10", "descriptionMK": "Поморски транспорт", "descriptionEN": "Maritime transport", "boxNumber": "25", "tooltip": "Основен код 1", "sortOrder": 1},
        {"code": "12", "descriptionMK": "Железнички вагон на поморски пловен објект", "descriptionEN": "Rail wagon on maritime vessel", "boxNumber": "25", "tooltip": "Проширен код 1", "sortOrder": 2},
        {"code": "16", "descriptionMK": "Моторно возило на поморски пловен објект", "descriptionEN": "Motor vehicle on maritime vessel", "boxNumber": "25", "tooltip": "Проширен код 1", "sortOrder": 3},
        {"code": "17", "descriptionMK": "Приколка или полу-приколка на поморски пловен објект", "descriptionEN": "Trailer or semi-trailer on maritime vessel", "boxNumber": "25", "tooltip": "Проширен код 1", "sortOrder": 4},
        {"code": "18", "descriptionMK": "Пловило за внатрешен воден сообраќај на поморски пловен објект", "descriptionEN": "Inland waterway vessel on maritime vessel", "boxNumber": "25", "tooltip": "Проширен код 1", "sortOrder": 5},
        {"code": "20", "descriptionMK": "Железнички транспорт", "descriptionEN": "Rail transport", "boxNumber": "25", "tooltip": "Основен код 2", "sortOrder": 6},
        {"code": "30", "descriptionMK": "Друмски транспорт", "descriptionEN": "Road transport", "boxNumber": "25", "tooltip": "Основен код 3", "sortOrder": 7},
        {"code": "40", "descriptionMK": "Воздушен транспорт", "descriptionEN": "Air transport", "boxNumber": "25", "tooltip": "Основен код 4", "sortOrder": 8},
        {"code": "50", "descriptionMK": "Поштенски транспорт", "descriptionEN": "Postal transport", "boxNumber": "25", "tooltip": "Основен код 5", "sortOrder": 9},
        {"code": "70", "descriptionMK": "Посебни видови на транспорт (цевовод или електрични водови)", "descriptionEN": "Fixed transport installations (pipelines, power lines)", "boxNumber": "25", "tooltip": "Основен код 7", "sortOrder": 10},
        {"code": "80", "descriptionMK": "Внатрешен воден транспорт", "descriptionEN": "Inland waterway transport", "boxNumber": "25", "tooltip": "Основен код 8", "sortOrder": 11},
        {"code": "90", "descriptionMK": "Сопствен погон", "descriptionEN": "Self-propulsion", "boxNumber": "25", "tooltip": "Основен код 9", "sortOrder": 12}
    ],
    
    # =========================================================================
    # BOX 31 - ВИД НА ПАКУВАЊЕ (најчести 30 од 100+)
    # =========================================================================
    "Box31_PackageType": [
        {"code": "CT", "descriptionMK": "Картон", "descriptionEN": "Carton", "boxNumber": "31", "sortOrder": 1},
        {"code": "PK", "descriptionMK": "Пакет", "descriptionEN": "Package", "boxNumber": "31", "sortOrder": 2},
        {"code": "BX", "descriptionMK": "Кутија", "descriptionEN": "Box", "boxNumber": "31", "sortOrder": 3},
        {"code": "PL", "descriptionMK": "Палета", "descriptionEN": "Pallet", "boxNumber": "31", "sortOrder": 4},
        {"code": "DR", "descriptionMK": "Буре", "descriptionEN": "Drum", "boxNumber": "31", "sortOrder": 5},
        {"code": "CS", "descriptionMK": "Кашон", "descriptionEN": "Case", "boxNumber": "31", "sortOrder": 6},
        {"code": "BA", "descriptionMK": "Вреќа", "descriptionEN": "Bag", "boxNumber": "31", "sortOrder": 7},
        {"code": "BL", "descriptionMK": "Бала, компримирана", "descriptionEN": "Bale, compressed", "boxNumber": "31", "sortOrder": 8},
        {"code": "BN", "descriptionMK": "Бала, некомпримирана", "descriptionEN": "Bale, not compressed", "boxNumber": "31", "sortOrder": 9},
        {"code": "LE", "descriptionMK": "Багаж", "descriptionEN": "Luggage", "boxNumber": "31", "sortOrder": 10},
        {"code": "AP", "descriptionMK": "Ампула, заштитена", "descriptionEN": "Ampoule, protected", "boxNumber": "31", "sortOrder": 11},
        {"code": "AM", "descriptionMK": "Ампула, незаштитена", "descriptionEN": "Ampoule, not protected", "boxNumber": "31", "sortOrder": 12},
        {"code": "IA", "descriptionMK": "Амбалажа, составна, дрвена", "descriptionEN": "Package, composite, wooden", "boxNumber": "31", "sortOrder": 13},
        {"code": "IC", "descriptionMK": "Амбалажа, составна, пластична", "descriptionEN": "Package, composite, plastic", "boxNumber": "31", "sortOrder": 14},
        {"code": "IF", "descriptionMK": "Амбалажа, цевчеста", "descriptionEN": "Package, tubular", "boxNumber": "31", "sortOrder": 15},
        {"code": "AT", "descriptionMK": "Атомизер (распрскувач)", "descriptionEN": "Atomizer (spray)", "boxNumber": "31", "sortOrder": 16},
        {"code": "BP", "descriptionMK": "Балон, заштитен", "descriptionEN": "Balloon, protected", "boxNumber": "31", "sortOrder": 17},
        {"code": "BF", "descriptionMK": "Балон, незаштитен", "descriptionEN": "Balloon, not protected", "boxNumber": "31", "sortOrder": 18},
        {"code": "CP", "descriptionMK": "Балон (сад) плетен, заштитен", "descriptionEN": "Cylinder, protected", "boxNumber": "31", "sortOrder": 19},
        {"code": "CO", "descriptionMK": "Балон (сад) плетен, незаштитен", "descriptionEN": "Cylinder, not protected", "boxNumber": "31", "sortOrder": 20},
        {"code": "OK", "descriptionMK": "Блок", "descriptionEN": "Block", "boxNumber": "31", "sortOrder": 21},
        {"code": "SO", "descriptionMK": "Бобина", "descriptionEN": "Spool", "boxNumber": "31", "sortOrder": 22},
        {"code": "JG", "descriptionMK": "Бокал", "descriptionEN": "Jug", "boxNumber": "31", "sortOrder": 23},
        {"code": "GB", "descriptionMK": "Боца за плин", "descriptionEN": "Gas bottle", "boxNumber": "31", "sortOrder": 24},
        {"code": "TI", "descriptionMK": "Буре (190 литри)", "descriptionEN": "Drum (190 litres)", "boxNumber": "31", "sortOrder": 25},
        {"code": "BU", "descriptionMK": "Буре големо (490,96 л)", "descriptionEN": "Barrel (490.96 l)", "boxNumber": "31", "sortOrder": 26},
        {"code": "CK", "descriptionMK": "Буре за вино, пиво", "descriptionEN": "Cask", "boxNumber": "31", "sortOrder": 27},
        {"code": "1G", "descriptionMK": "Буре од влакна", "descriptionEN": "Fibre drum", "boxNumber": "31", "sortOrder": 28},
        {"code": "1D", "descriptionMK": "Буре од шпертплоча", "descriptionEN": "Plywood drum", "boxNumber": "31", "sortOrder": 29},
        {"code": "1W", "descriptionMK": "Буре, дрвено", "descriptionEN": "Wooden drum", "boxNumber": "31", "sortOrder": 30}
    ],
    
    # =========================================================================
    # BOX 37 - ПРОЦЕДУРА (Царинска постапка)
    # =========================================================================
    "Box37_ProcedureCode": [
        {"code": "40 00", "descriptionMK": "Пуштање во слободен промет", "descriptionEN": "Release for free circulation", "boxNumber": "37", "tooltip": "Нормален увоз со плаќање на сите давачки", "sortOrder": 1},
        {"code": "42 00", "descriptionMK": "Увоз за облагородување - Одложено плаќање", "descriptionEN": "Inward processing - Suspension system", "boxNumber": "37", "tooltip": "LON - Без плаќање на давачки при увоз, реекспорт задолжителен", "sortOrder": 2},
        {"code": "51 00", "descriptionMK": "Увоз за облагородување - Враќање на давачки", "descriptionEN": "Inward processing - Drawback system", "boxNumber": "37", "tooltip": "LON - Плаќање на давачки, враќање при реекспорт", "sortOrder": 3},
        {"code": "31 51", "descriptionMK": "Реекспорт на стока увезена за облагородување", "descriptionEN": "Re-export of goods imported for inward processing", "boxNumber": "37", "tooltip": "LON - Реекспорт на компензациски производи", "sortOrder": 4},
        {"code": "10 00", "descriptionMK": "Извоз со трајно отстранување", "descriptionEN": "Export with permanent removal", "boxNumber": "37", "tooltip": "Нормален извоз", "sortOrder": 5},
        {"code": "21 00", "descriptionMK": "Привремен извоз", "descriptionEN": "Temporary export", "boxNumber": "37", "tooltip": "Извоз со намера за враќање", "sortOrder": 6},
        {"code": "53 00", "descriptionMK": "Царински складови", "descriptionEN": "Customs warehousing", "boxNumber": "37", "tooltip": "Складирање со одложено плаќање на давачки", "sortOrder": 7},
        {"code": "61 00", "descriptionMK": "Привремен увоз", "descriptionEN": "Temporary admission", "boxNumber": "37", "tooltip": "Делумно или целосно ослободување од увозни давачки", "sortOrder": 8},
        {"code": "63 00", "descriptionMK": "Реекспорт", "descriptionEN": "Re-export", "boxNumber": "37", "tooltip": "Извоз на стока претходно увезена привремено", "sortOrder": 9},
        {"code": "71 00", "descriptionMK": "Транзит - Комунитарна стока", "descriptionEN": "Transit - Community goods", "boxNumber": "37", "tooltip": "T2 режим", "sortOrder": 10},
        {"code": "91 00", "descriptionMK": "Транзит - Странска стока", "descriptionEN": "Transit - Non-community goods", "boxNumber": "37", "tooltip": "T1 режим", "sortOrder": 11}
    ],
    
    # =========================================================================
    # BOX 44 - ПОСЕБНИ ЗАБЕЛЕШКИ / ДОКУМЕНТИ (најважни)
    # =========================================================================
    "Box44_DocumentType": [
        {"code": "N730", "descriptionMK": "Дозвола за увоз за облагородување", "descriptionEN": "Inward processing authorization", "boxNumber": "44", "tooltip": "Задолжително за процедури 42 00, 51 00", "sortOrder": 1},
        {"code": "N380", "descriptionMK": "Профактура", "descriptionEN": "Proforma invoice", "boxNumber": "44", "tooltip": "Привремена фактура", "sortOrder": 2},
        {"code": "N703", "descriptionMK": "Договор", "descriptionEN": "Contract", "boxNumber": "44", "tooltip": "Тргувачки договор", "sortOrder": 3},
        {"code": "N785", "descriptionMK": "Извозна дозвола", "descriptionEN": "Export licence", "boxNumber": "44", "tooltip": "За ограничени стоки", "sortOrder": 4},
        {"code": "N935", "descriptionMK": "Увозна дозвола", "descriptionEN": "Import licence", "boxNumber": "44", "tooltip": "За ограничени стоки", "sortOrder": 5},
        {"code": "N954", "descriptionMK": "Сертификат за здравствена инспекција", "descriptionEN": "Sanitary certificate", "boxNumber": "44", "tooltip": "За храна, растенија, животни", "sortOrder": 6},
        {"code": "C505", "descriptionMK": "Декларација за вредност", "descriptionEN": "Declaration of value", "boxNumber": "44", "tooltip": "DV1 образец", "sortOrder": 7},
        {"code": "C644", "descriptionMK": "Сертификат за потекло", "descriptionEN": "Certificate of origin", "boxNumber": "44", "tooltip": "EUR.1, Form A, CO", "sortOrder": 8},
        {"code": "Y024", "descriptionMK": "Декларација за потекло врз фактура", "descriptionEN": "Origin declaration on invoice", "boxNumber": "44", "tooltip": "За овластени извозници", "sortOrder": 9},
        {"code": "C001", "descriptionMK": "Комерцијална фактура", "descriptionEN": "Commercial invoice", "boxNumber": "44", "tooltip": "Задолжителен документ", "sortOrder": 10},
        {"code": "N704", "descriptionMK": "Транспортен документ", "descriptionEN": "Transport document", "boxNumber": "44", "tooltip": "CMR, AWB, B/L", "sortOrder": 11}
    ],
    
    # =========================================================================
    # BOX 47 - МЕТОД НА ПРЕСМЕТКА (Царинска вредност)
    # =========================================================================
    "Box47_CalculationMethod": [
        {"code": "1", "descriptionMK": "Метод 1 - Трансакциска вредност", "descriptionEN": "Method 1 - Transaction value", "boxNumber": "47", "tooltip": "Цена навистина платена или за плаќање", "sortOrder": 1},
        {"code": "2", "descriptionMK": "Метод 2 - Трансакциска вредност на идентични стоки", "descriptionEN": "Method 2 - Transaction value of identical goods", "boxNumber": "47", "tooltip": "Алтернативен метод", "sortOrder": 2},
        {"code": "3", "descriptionMK": "Метод 3 - Трансакциска вредност на слични стоки", "descriptionEN": "Method 3 - Transaction value of similar goods", "boxNumber": "47", "tooltip": "Алтернативен метод", "sortOrder": 3},
        {"code": "4", "descriptionMK": "Метод 4 - Дедуктивен метод", "descriptionEN": "Method 4 - Deductive method", "boxNumber": "47", "tooltip": "Врз основа на продажна цена", "sortOrder": 4},
        {"code": "5", "descriptionMK": "Метод 5 - Компутативен метод", "descriptionEN": "Method 5 - Computed method", "boxNumber": "47", "tooltip": "Врз основа на производни трошоци", "sortOrder": 5},
        {"code": "6", "descriptionMK": "Метод 6 - Резервен метод", "descriptionEN": "Method 6 - Fall-back method", "boxNumber": "47", "tooltip": "Флексибилна примена на други методи", "sortOrder": 6}
    ],
    
    # =========================================================================
    # LON СПЕЦИФИЧНИ - Операции за увоз за облагородување
    # =========================================================================
    "LON_OperationType": [
        {"code": "Обработка", "descriptionMK": "Обработка", "descriptionEN": "Processing", "boxNumber": None, "tooltip": "Операции со кои се менува природата на стоката", "sortOrder": 1},
        {"code": "Преработка", "descriptionMK": "Преработка", "descriptionEN": "Manufacturing", "boxNumber": None, "tooltip": "Операции со кои се добиваат нови производи", "sortOrder": 2},
        {"code": "Склопување", "descriptionMK": "Склопување", "descriptionEN": "Assembly", "boxNumber": None, "tooltip": "Операции со кои се составуваат комплетни производи од делови", "sortOrder": 3},
        {"code": "Поправка", "descriptionMK": "Поправка", "descriptionEN": "Repair", "boxNumber": None, "tooltip": "Операции со кои се враќа функционалноста на оштетени стоки", "sortOrder": 4}
    ],
    
    # =========================================================================
    # LON СПЕЦИФИЧНИ - Економски услови
    # =========================================================================
    "LON_EconomicCondition": [
        {"code": "A1", "descriptionMK": "Активно облагородување", "descriptionEN": "Active processing", "boxNumber": None, "tooltip": "Облагородувачот врши обработка во свои капацитети", "sortOrder": 1},
        {"code": "B1", "descriptionMK": "Подизведување", "descriptionEN": "Subcontracting", "boxNumber": None, "tooltip": "Облагородувачот ангажира трети лица за обработка", "sortOrder": 2},
        {"code": "C1", "descriptionMK": "Редовен извоз", "descriptionEN": "Standard export", "boxNumber": None, "tooltip": "Без посебни услови", "sortOrder": 3}
    ],
    
    # =========================================================================
    # LON СПЕЦИФИЧНИ - Статус на авторизација
    # =========================================================================
    "LON_AuthorizationStatus": [
        {"code": "Draft", "descriptionMK": "Нацрт", "descriptionEN": "Draft", "boxNumber": None, "tooltip": "Авторизацијата е во подготовка", "sortOrder": 1},
        {"code": "Submitted", "descriptionMK": "Поднесена", "descriptionEN": "Submitted", "boxNumber": None, "tooltip": "Авторизацијата е поднесена на царинската управа", "sortOrder": 2},
        {"code": "Approved", "descriptionMK": "Одобрена", "descriptionEN": "Approved", "boxNumber": None, "tooltip": "Авторизацијата е одобрена и активна", "sortOrder": 3},
        {"code": "Rejected", "descriptionMK": "Одбиена", "descriptionEN": "Rejected", "boxNumber": None, "tooltip": "Авторизацијата е одбиена од царинската управа", "sortOrder": 4},
        {"code": "Expired", "descriptionMK": "Истечена", "descriptionEN": "Expired", "boxNumber": None, "tooltip": "Авторизацијата повеќе не важи", "sortOrder": 5}
    ]
}


def main():
    """Генерира комплетни шифрарници со Box броеви и tooltips"""
    
    print("=" * 80)
    print("🔧 ГЕНЕРИРАЊЕ НА КОМПЛЕТНИ ШИФРАРНИЦИ")
    print("=" * 80)
    
    # Пресметај вкупно кодови
    total_codes = sum(len(codes) for codes in codelists.values())
    
    # Креирај JSON структура за извоз
    output = {
        "metadata": {
            "source": "ПРАВИЛНИК ЗА НАЧИНОТ НА ПОПОЛНУВАЊЕ НА ЦАРИНСКАТА ДЕКЛАРАЦИЈА - Поглавје II Шифри",
            "generated": datetime.now().isoformat(),
            "total_codelists": len(codelists),
            "total_codes": total_codes,
            "version": "2.0",
            "notes": [
                "Сите шифрарници со Box број за dropdown UI елементи",
                "Tooltip текст за контекстуална помош",
                "Опис на македонски и англиски јазик",
                "ISO стандарди за земји, валути, транспорт"
            ]
        },
        "codelists": {}
    }
    
    # Конвертирај во формат за база
    for list_type, codes in codelists.items():
        output["codelists"][list_type] = {
            "listType": list_type,
            "boxNumber": codes[0].get("boxNumber") if codes else None,
            "totalCodes": len(codes),
            "codes": codes
        }
    
    # Зачувај во JSON
    output_path = os.path.join(
        os.path.dirname(os.path.dirname(__file__)),
        "processed",
        "lon_codelists_complete.json"
    )
    
    with open(output_path, 'w', encoding='utf-8') as f:
        json.dump(output, f, ensure_ascii=False, indent=2)
    
    # Испечати резултат
    print(f"\n✅ Успешно креирани {len(codelists)} шифрарници со вкупно {total_codes} кодови")
    print("\n📊 Детали по Box број:")
    
    box_stats = {}
    for list_type, codes in codelists.items():
        box_num = codes[0].get("boxNumber") if codes else "N/A"
        if box_num not in box_stats:
            box_stats[box_num] = {"lists": 0, "codes": 0}
        box_stats[box_num]["lists"] += 1
        box_stats[box_num]["codes"] += len(codes)
    
    for box_num in sorted(box_stats.keys(), key=lambda x: (x is None, x)):
        stats = box_stats[box_num]
        box_label = f"Box {box_num}" if box_num else "LON Специфични"
        print(f"   └─ {box_label:20s}: {stats['lists']} листи, {stats['codes']} кодови")
    
    print(f"\n💾 Зачувано во: {output_path}")
    
    # Прикажи примери
    print("\n📋 ПРИМЕРИ:")
    for list_type in ["Box37_ProcedureCode", "Box44_DocumentType", "Box20_IncoTerms"]:
        if list_type in codelists:
            codes = codelists[list_type]
            print(f"\n   {list_type}:")
            for code_obj in codes[:3]:
                print(f"      • {code_obj['code']:10s} - {code_obj['descriptionMK']}")
                if code_obj.get('tooltip'):
                    print(f"        └─ 💡 {code_obj['tooltip']}")


if __name__ == "__main__":
    main()
