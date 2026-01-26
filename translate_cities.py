#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Script to translate city names to Macedonian
"""

import csv

# Dictionary for common city name translations
CITY_TRANSLATIONS = {
    # English to Macedonian
    "Orion": "Орион",
    "Unknown": "Непознато",
    "НЕПОЗНАТ": "Непознато",
    
    # Common patterns
    "Prince Albert": "Принс Алберт",
    "Edinburgh of the Seven Seas": "Единбург на Седумте Мориња",
    "Belo Horizonte": "Бело Хоризонте",
    "El Prat de Llobregat": "Ел Прат де Љобрегат",
    "A Coruña": "Ла Коруња",
    "Don Torcuato": "Дон Торкуато",
    "Ad Diwem": "Ад Дивем",
    "Waltham Abbey": "Волтам Ебеј",
    "Tha Maka": "Та Мака",
    "San Quintin": "Сан Квинтин",
    "Qal'at an Nakhl": "Калат ан Нахл",
    "Torre Maggiore": "Торе Мађоре",
}

def transliterate_to_cyrillic(text):
    """
    Transliterate Latin text to Macedonian Cyrillic
    """
    # Latin to Macedonian Cyrillic mapping
    mapping = {
        'a': 'а', 'A': 'А',
        'b': 'б', 'B': 'Б',
        'c': 'ц', 'C': 'Ц',
        'd': 'д', 'D': 'Д',
        'e': 'е', 'E': 'Е',
        'f': 'ф', 'F': 'Ф',
        'g': 'г', 'G': 'Г',
        'h': 'х', 'H': 'Х',
        'i': 'и', 'I': 'И',
        'j': 'ј', 'J': 'Ј',
        'k': 'к', 'K': 'К',
        'l': 'л', 'L': 'Л',
        'm': 'м', 'M': 'М',
        'n': 'н', 'N': 'Н',
        'o': 'о', 'O': 'О',
        'p': 'п', 'P': 'П',
        'r': 'р', 'R': 'Р',
        's': 'с', 'S': 'С',
        't': 'т', 'T': 'Т',
        'u': 'у', 'U': 'У',
        'v': 'в', 'V': 'В',
        'z': 'з', 'Z': 'З',
    }
    
    # Handle special combinations
    text = text.replace('sh', 'ш').replace('Sh', 'Ш').replace('SH', 'Ш')
    text = text.replace('ch', 'ч').replace('Ch', 'Ч').replace('CH', 'Ч')
    text = text.replace('zh', 'ж').replace('Zh', 'Ж').replace('ZH', 'Ж')
    text = text.replace('dz', 'џ').replace('Dz', 'Џ').replace('DZ', 'Џ')
    text = text.replace('kj', 'ќ').replace('Kj', 'Ќ').replace('KJ', 'Ќ')
    text = text.replace('gj', 'ѓ').replace('Gj', 'Ѓ').replace('GJ', 'Ѓ')
    text = text.replace('lj', 'љ').replace('Lj', 'Љ').replace('LJ', 'Љ')
    text = text.replace('nj', 'њ').replace('Nj', 'Њ').replace('NJ', 'Њ')
    
    # Apply single character mapping
    result = []
    for char in text:
        result.append(mapping.get(char, char))
    
    return ''.join(result)

def translate_city(city_name):
    """
    Translate city name to Macedonian
    """
    # Check if we have a direct translation
    if city_name in CITY_TRANSLATIONS:
        return CITY_TRANSLATIONS[city_name]
    
    # Check if already in Cyrillic
    if any('\u0400' <= c <= '\u04FF' for c in city_name):
        return city_name
    
    # Otherwise transliterate
    return transliterate_to_cyrillic(city_name)

def main():
    input_file = 'Cities Prevod.csv'
    output_file = 'Cities Prevod.csv'
    
    # Read the CSV file
    rows = []
    with open(input_file, 'r', encoding='utf-8') as f:
        reader = csv.reader(f, delimiter=';')
        for row in reader:
            if len(row) >= 2:
                guid = row[0]
                city_original = row[1]
                city_macedonian = translate_city(city_original)
                rows.append([guid, city_original, city_macedonian])
            elif len(row) == 1:
                # Handle case with only GUID
                rows.append([row[0], '', ''])
    
    # Write back to CSV with new column
    with open(output_file, 'w', encoding='utf-8', newline='') as f:
        writer = csv.writer(f, delimiter=';')
        for row in rows:
            writer.writerow(row)
    
    print(f"✓ Преведени {len(rows)} градови")
    print(f"✓ Фајлот е ажуриран: {output_file}")
    
    # Show sample translations
    print("\nПримери на преведени градови:")
    for i in range(min(20, len(rows))):
        if rows[i][1]:
            print(f"  {rows[i][1]} → {rows[i][2]}")

if __name__ == '__main__':
    main()
