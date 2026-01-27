#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Script to translate city names to Macedonian
"""

import csv
import re

# Dictionary for city name translations - how they're actually pronounced in Macedonian
CITY_TRANSLATIONS = {
    # Already in Macedonian
    "НЕПОЗНАТ": "Непознато",
    "Unknown": "Непознато",
    
    # Major world cities - proper Macedonian names
    "London": "Лондон",
    "Paris": "Париз",
    "Moscow": "Москва",
    "Beijing": "Пекинг",
    "Tokyo": "Токио",
    "New York": "Њујорк",
    "Los Angeles": "Лос Анџелес",
    "Chicago": "Чикаго",
    "Berlin": "Берлин",
    "Rome": "Рим",
    "Madrid": "Мадрид",
    "Barcelona": "Барселона",
    "Vienna": "Виена",
    "Prague": "Прага",
    "Warsaw": "Варшава",
    "Budapest": "Будимпешта",
    "Athens": "Атина",
    "Cairo": "Каиро",
    "Istanbul": "Истанбул",
    "Dubai": "Дубаи",
    "Mumbai": "Мумбаи",
    "Delhi": "Делхи",
    "Shanghai": "Шангај",
    "Hong Kong": "Хонг Конг",
    "Sydney": "Сиднеј",
    "Melbourne": "Мелбурн",
    "Toronto": "Торонто",
    "Montreal": "Монреал",
    "Vancouver": "Ванкувер",
    "Mexico City": "Мексико Сити",
    "Buenos Aires": "Буенос Аирес",
    "Rio de Janeiro": "Рио де Жанеиро",
    "São Paulo": "Сао Паоло",
    "Lisbon": "Лисабон",
    "Brussels": "Брисел",
    "Amsterdam": "Амстердам",
    "Copenhagen": "Копенхаген",
    "Stockholm": "Стокхолм",
    "Helsinki": "Хелсинки",
    "Oslo": "Осло",
    "Dublin": "Даблин",
    "Edinburgh": "Единбург",
    "Geneva": "Женева",
    "Zurich": "Цирих",
    "Munich": "Минхен",
    "Frankfurt": "Франкфурт",
    "Hamburg": "Хамбург",
    "Cologne": "Келн",
    "Milan": "Милано",
    "Naples": "Напуљ",
    "Venice": "Венеција",
    "Florence": "Фиренца",
    "Marseille": "Марсеј",
    "Lyon": "Лион",
    "Bordeaux": "Бордо",
    "Nice": "Ница",
    "Jerusalem": "Ерусалим",
    "Tel Aviv": "Тел Авив",
    "Singapore": "Сингапур",
    "Bangkok": "Бангкок",
    "Manila": "Манила",
    "Jakarta": "Џакарта",
    "Seoul": "Сеул",
    "Pyongyang": "Пјонгјанг",
    "Hanoi": "Ханој",
    "Ho Chi Minh City": "Хо Ши Мин",
    
    # Common patterns from CSV
    "Prince Albert": "Принс Алберт",
    "Edinburgh of the Seven Seas": "Единбург на Седумте Мориња",
    "Belo Horizonte": "Бело Оризонте",
    "El Prat de Llobregat": "Ел Прат де Љобрегат",
    "A Coruña": "Ла Коруња",
    "Don Torcuato": "Дон Торквато",
    "Waltham Abbey": "Волтам Аби",
    "San Quintin": "Сан Квентин",
    "Torre Maggiore": "Торе Маџоре",
}

def phonetic_transcribe(text):
    """
    Phonetic transcription to Macedonian Cyrillic (how it's pronounced)
    """
    if not text:
        return text
        
    result = text
    
    # Multi-character combinations first (order matters!)
    replacements = [
        # English phonetics
        (r'tion\b', 'шн'),
        (r'tion', 'шон'),
        (r'sion\b', 'жн'),
        (r'sion', 'жон'),
        ('ough', 'аф'),
        ('augh', 'оф'),
        ('eigh', 'еј'),
        
        # Consonant clusters
        ('tch', 'ч'),
        ('ch', 'ч'),
        ('sh', 'ш'),
        ('zh', 'ж'),
        ('th', 'т'),  # English th -> t
        ('ph', 'ф'),
        ('gh', 'г'),
        ('ck', 'к'),
        ('qu', 'кв'),
        ('x', 'кс'),
        
        # Special combinations
        ('sch', 'ш'),
        ('cz', 'ч'),
        ('sz', 'с'),
        ('dj', 'џ'),
        ('dz', 'џ'),
        ('gj', 'џ'),
        ('kj', 'ќ'),
        ('lj', 'љ'),
        ('nj', 'њ'),
        
        # Double consonants
        ('bb', 'б'),
        ('cc', 'к'),
        ('dd', 'д'),
        ('ff', 'ф'),
        ('gg', 'г'),
        ('ll', 'л'),
        ('mm', 'м'),
        ('nn', 'н'),
        ('pp', 'п'),
        ('rr', 'р'),
        ('ss', 'с'),
        ('tt', 'т'),
        ('zz', 'з'),
    ]
    
    for old, new in replacements:
        if old.startswith('\\') or old.endswith('\\'):
            # Regex pattern
            result = re.sub(old, new, result, flags=re.IGNORECASE)
        else:
            # Case-insensitive replace
            result = re.sub(re.escape(old), new, result, flags=re.IGNORECASE)
    
    # Single character mapping
    mapping = {
        'a': 'а', 'A': 'А',
        'b': 'б', 'B': 'Б',
        'c': 'к', 'C': 'К',  # c -> k sound
        'd': 'д', 'D': 'Д',
        'e': 'е', 'E': 'Е',
        'f': 'ф', 'F': 'Ф',
        'g': 'г', 'G': 'Г',
        'h': 'х', 'H': 'Х',
        'i': 'и', 'I': 'И',
        'j': 'џ', 'J': 'Џ',  # English j -> dzh sound
        'k': 'к', 'K': 'К',
        'l': 'л', 'L': 'Л',
        'm': 'м', 'M': 'М',
        'n': 'н', 'N': 'Н',
        'o': 'о', 'O': 'О',
        'p': 'п', 'P': 'П',
        'q': 'к', 'Q': 'К',
        'r': 'р', 'R': 'Р',
        's': 'с', 'S': 'С',
        't': 'т', 'T': 'Т',
        'u': 'у', 'U': 'У',
        'v': 'в', 'V': 'В',
        'w': 'в', 'W': 'В',  # w -> v sound
        'y': 'ј', 'Y': 'Ј',  # y at start/middle -> j sound
        'z': 'з', 'Z': 'З',
    }
    
    # Apply single character mapping
    final_result = []
    for char in result:
        final_result.append(mapping.get(char, char))
    
    transcribed = ''.join(final_result)
    
    # Capitalize each word in the city name
    if transcribed:
        words = transcribed.split()
        capitalized_words = []
        for word in words:
            # Keep special characters, capitalize letters
            if word and len(word) > 0:
                # Find first letter
                first_letter_idx = 0
                for i, c in enumerate(word):
                    if c.isalpha():
                        first_letter_idx = i
                        break
                
                if first_letter_idx == 0:
                    capitalized_words.append(word[0].upper() + word[1:].lower())
                else:
                    # Handle case where word starts with non-letter (e.g., 'o-Mar)
                    capitalized_words.append(
                        word[:first_letter_idx] + 
                        word[first_letter_idx].upper() + 
                        word[first_letter_idx+1:].lower()
                    )
            else:
                capitalized_words.append(word)
        
        transcribed = ' '.join(capitalized_words)
    
    return transcribed

def translate_city(city_name):
    """
    Translate city name to Macedonian (phonetic pronunciation)
    """
    # Check if we have a direct translation
    if city_name in CITY_TRANSLATIONS:
        return CITY_TRANSLATIONS[city_name]
    
    # Check if already in Cyrillic - keep as is
    if any('\u0400' <= c <= '\u04FF' for c in city_name):
        return city_name
    
    # Otherwise do phonetic transcription
    return phonetic_transcribe(city_name)

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
