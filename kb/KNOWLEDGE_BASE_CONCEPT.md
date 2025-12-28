# Knowledge Base Концепт за LON Систем

## Анализа на постоечки ресурси

### 📊 Податоци во `kb/Raw_Files/`:

#### 1. **Регулативни документи** (PDF - 15MB total)
- `Закон за царинење.pdf` (1.0MB) - Основен законски оквир
- `УРЕДБА ЗА СПРОВЕДУВАЊЕ НА ЦАРИНСКИОТ ЗАКОН.pdf` (1.3MB) - Имплементација
- `ПРАВИЛНИК ЗА НАЧИНОТ НА ПОПОЛНУВАЊЕ НА ЦАРИНСКАТА ДЕКЛАРАЦИЈА.pdf` (826KB, 168 страници) - **КРИТИЧЕН** за валидација
- `UREDBA_ZA_AVTONOMNI_MERKI___119-2005_.pdf` (145KB)
- `PravilnikVidNadomestociPrecistenTekst.pdf` (267KB)

#### 2. **Упатства и прирачници**
- `TraderManual_CALC_EN_v.1.4_mk.pdf` (2.0MB) - CALC систем мануал
- `Прирачник за ТАРИМ.pdf` (2.2MB) - TARIC упатство
- `P2MK1_4_1.1.MK1_upatstvo_za_carinska_vrednost.pdf` (1.8MB) - Вреднување
- `P2MK1.4.1.1.MK2_deklaracija_na_vrednost.pdf` (334KB)

#### 3. **Процесни документи**
- `uvoz.za.oblagoroduvanje.pdf` (1.7MB) - Облагородување процедури
- `Увоз за облагородување.docx` (17KB)
- `Povtore_izvoz_nasoka_07062019.pdf` (372KB) - Повторен извоз
- `Podadok_za_razdolzuvanje_garancija.pdf` (66KB) - Гаранции
- `prilozi.uvoz28.02.doc` (450KB) - Прилози за увоз

#### 4. **Структурирани податоци** (Excel)
- **`TARIC.xlsx`** (1.2MB)
  - **10,307 записи** - тарифни ознаки
  - 21 колони: Tariff number, Description, Customs rate, VAT, Unit measure, итн.
  - Структурирани царински стапки и мерни единици
  
- **`Spisok na Regulativi KN 15.xlsx`** (807KB)
  - **1,809 регулативи** - специфични распоредувања на стоки
  - CELEX број, тарифна ознака, опис на македонски и англиски
  - Правен основ на распоредување

---

## 🎯 Предложен Концепт: Multi-Layer Knowledge Base Architecture

### Слој 1: **Structured Data Layer** (SQL Database)
Структурирани податоци кои се директно интегрирани во апликацијата.

#### Табели:
```sql
-- Веќе постои: Items, UoM, Warehouses

-- Ново: TARIC база
CREATE TABLE TariffCodes (
    Id uniqueidentifier PRIMARY KEY,
    TariffNumber nvarchar(10) NOT NULL UNIQUE,
    TARBR nvarchar(4),
    Description nvarchar(500),
    CustomsRate decimal(5,2),
    VATRate decimal(5,2),
    UnitMeasure nvarchar(10),
    IsActive bit DEFAULT 1
);

-- Регулативи и специјални режими
CREATE TABLE CustomsRegulations (
    Id uniqueidentifier PRIMARY KEY,
    CelexNumber nvarchar(50),
    OfficialGazetteRef nvarchar(100),
    TariffNumber nvarchar(10),
    DescriptionMK nvarchar(max),
    DescriptionEN nvarchar(max),
    LegalBasis nvarchar(max),
    EffectiveDate datetime2,
    ExpiryDate datetime2 NULL
);

-- Правила за валидација (извлечени од правилник)
CREATE TABLE DeclarationRules (
    Id uniqueidentifier PRIMARY KEY,
    RuleCode nvarchar(50) UNIQUE,
    FieldName nvarchar(100), -- Box01, Box02, etc.
    RuleType nvarchar(50), -- Required, Format, CrossField, ValueList
    ValidationLogic nvarchar(max), -- JSON or SQL expression
    ErrorMessage nvarchar(500),
    Severity nvarchar(20), -- Error, Warning, Info
    ReferenceDocument nvarchar(200),
    IsActive bit DEFAULT 1
);

-- Код листи (шифри од правилник)
CREATE TABLE CodeLists (
    Id uniqueidentifier PRIMARY KEY,
    ListType nvarchar(50), -- ProcedureCode, DocumentType, Country, etc.
    Code nvarchar(20),
    DescriptionMK nvarchar(200),
    DescriptionEN nvarchar(200),
    ParentCode nvarchar(20) NULL, -- За хиерархиски листи
    IsActive bit DEFAULT 1,
    UNIQUE (ListType, Code)
);
```

**Извор**: `TARIC.xlsx`, `Spisok na Regulativi KN 15.xlsx`

---

### Слој 2: **Rule Engine Layer** (Business Logic)
Smart валидација која автоматски проверува compliance.

#### Имплементација:
```csharp
// LON.Application/Customs/Validators/DeclarationValidator.cs
public class DeclarationRuleEngine
{
    public ValidationResult ValidateDeclaration(CustomsDeclaration declaration)
    {
        // 1. Field-level validation (формати, задолжителни полиња)
        // 2. Cross-field validation (Box 40 мора да одговара на Box 33)
        // 3. Regulatory compliance (дали процедура е дозволена за тарифа)
        // 4. Data consistency (суми, единици, итн.)
    }
}
```

**Примери на правила**:
- Box 40: Duty rate мора да одговара на TARIC табела за дадената тарифна ознака
- Box 33: Commodity code мора да биде валиден 10-цифрен TARIC код
- Box 37: Procedure code мора да е од одобрената листа (40 00, 42 00, 51 00...)
- Box 44: Documents мора да се од дозволени типови (N380, N703, N730...)

---

### Слој 3: **Document Vector Store** (RAG - Retrieval Augmented Generation)
Семантичко пребарување низ регулативни документи за контекстуална помош.

#### Технологија:
- **Vector Database**: pgvector (PostgreSQL extension) или Azure AI Search
- **Embeddings**: OpenAI text-embedding-3-small или локален модел
- **Chunking Strategy**: 
  - Правилник: по член (Article-level chunks)
  - Упатства: по секција/подзаглавие
  - Прирачници: по теми (procedure-specific)

#### Структура:
```json
{
  "chunk_id": "pravilnik_chlen_15",
  "document_source": "ПРАВИЛНИК ЗА НАЧИНОТ НА ПОПОЛНУВАЊЕ",
  "section": "Член 15 - Box 33 (Commodity Code)",
  "content": "Тарифната ознака се состои од 10 цифри...",
  "metadata": {
    "article": 15,
    "box_number": 33,
    "category": "validation_rule",
    "effective_date": "2019-06-01"
  },
  "embedding": [0.123, -0.456, ...] // 1536 dimensions
}
```

---

### Слој 4: **AI Assistant Layer** (User-facing)
Интелигентен асистент кој помага на корисникот за време на пополнување.

#### Функционалности:

1. **Contextual Help** (Помош по полиња)
   ```
   User: Што треба да внесам во Box 40?
   AI: Box 40 содржи царински надомест за вашата стока. 
       За тарифа 0101210000 (живи коњи), стапката е 0% 
       според TARIC.
       Референца: Правилник, Член 47
   ```

2. **Smart Validation** (Real-time)
   ```
   User: [внесува commodity code 0101999999]
   AI: ⚠️ Неправилен код! Тарифна ознака 0101999999 не 
       постои во TARIC базата. Дали мислевте на:
       - 0101299000 (Други коњи, живи)
       - 0101300000 (Магариња, живи)?
   ```

3. **Document Suggestions** (Автоматско препознавање)
   ```
   System: [детектира procedure code "42 00" - облагородување]
   AI: 💡 За процедура 42 00 (увоз за облагородување) ви 
       треба:
       ✓ Одобрение (Document N730)
       ✓ Проформа фактура
       ✓ Трговски договор
       Референца: uvoz.za.oblagoroduvanje.pdf, стр. 12
   ```

4. **Regulatory Updates** (Известувања)
   ```
   AI: ⓘ Нова регулатива (CELEX 32013R0729) влегува на сила од 
       01.01.2026 и влијае на тарифа 0307 99 80. 
       Преглед: [link]
   ```

5. **Procedure Wizard** (Водич чекор-по-чекор)
   ```
   AI: Изгледа дека пријавувате повторен извоз. Ајде чекор по чекор:
       1️⃣ Внесете број на претходна увозна декларација
       2️⃣ Проверка: Гаранцијата треба да е раздолжена
       3️⃣ Документ N785 (извозна дозвола) е задолжителен
   ```

---

## 🔧 Имплементација - Фази

### **Фаза 1: Data Loading (2-3 недели)**
```
kb/Raw_Files/
│
├── scripts/
│   ├── import_taric.py          → TariffCodes табела
│   ├── import_regulations.py    → CustomsRegulations табела
│   ├── extract_rules.py         → DeclarationRules табела (manual parsing)
│   └── parse_codelists.py       → CodeLists табела
│
└── processed/
    ├── taric.json               → 10K+ тарифи
    ├── regulations.json         → 1.8K регулативи
    ├── rules.json               → 100+ валидациски правила
    └── codelists.json           → 20+ листи (countries, procedures, documents...)
```

**Задачи**:
- ✅ Parse Excel фајлови (openpyxl)
- ✅ Extract rules од PDF (PyPDF2 + regex + manual review)
- ✅ Seed во база (EF Core migration)

---

### **Фаза 2: Rule Engine (2 недели)**
```csharp
// LON.Application/Customs/Rules/
├── IDeclarationRule.cs
├── RequiredFieldRule.cs
├── FormatValidationRule.cs
├── TariffValidationRule.cs
├── ProcedureDocumentRule.cs
└── CrossFieldValidationRule.cs

// Usage in Command Handler:
public async Task<Result> Handle(CreateDeclarationCommand request)
{
    var rules = _ruleRepository.GetActiveRules();
    var validationResult = _ruleEngine.Validate(request.Declaration, rules);
    
    if (!validationResult.IsValid)
        return Result.Failure(validationResult.Errors);
    
    // Continue with declaration creation...
}
```

---

### **Фаза 3: Vector Store + RAG (3 недели)**
```
kb/
├── embeddings/
│   ├── chunks/                  → JSON chunks од PDF
│   │   ├── pravilnik_001.json
│   │   ├── pravilnik_002.json
│   │   └── ...
│   │
│   └── vectordb/                → pgvector или ChromaDB
│
└── scripts/
    ├── chunk_documents.py       → Split PDF into semantic chunks
    ├── generate_embeddings.py   → OpenAI API или sentence-transformers
    └── ingest_vectors.py        → Store in vector DB
```

**API Integration**:
```csharp
// LON.Application/Customs/Queries/GetContextualHelpQuery.cs
public class GetContextualHelpQuery : IRequest<string>
{
    public string BoxNumber { get; set; }
    public string UserQuestion { get; set; }
    public CustomsDeclaration? CurrentDeclaration { get; set; }
}

// Handler uses:
// 1. Vector search за релевантни chunks
// 2. GPT-4 за генерирање одговор со контекст
// 3. Citation линкови кон оригинални документи
```

---

### **Фаза 4: UI Integration (2 недели)**
```typescript
// frontend/web/src/components/customs/SmartDeclarationForm.tsx

// Real-time validation tooltip
<InputField
  name="box33_commodity_code"
  onBlur={(value) => validateField('box33', value)}
  helpText={<AIAssistant boxNumber="33" />}
/>

// AI Assistant sidebar
<Sidebar>
  <ChatBot 
    context={currentDeclaration}
    knowledgeBase="customs"
  />
</Sidebar>

// Warnings panel
<ValidationPanel>
  {warnings.map(w => (
    <Alert severity={w.severity}>
      {w.message}
      <Link to={w.referenceDoc}>Повеќе...</Link>
    </Alert>
  ))}
</ValidationPanel>
```

---

## 📊 Очекувани резултати

### Квалитативни:
- ✅ **100% compliance** со правилник за декларации
- ✅ **Автоматска валидација** на сите 54 бокса
- ✅ **Контекстуална помош** на македонски јазик
- ✅ **Намалување на грешки** за 70-80%
- ✅ **Забрзување на процес** - од 30 мин на 10 мин

### Квантитативни:
- 10,307 тарифни ознаки во база (од TARIC)
- 1,809 специфични регулативи (од Spisok na Regulativi)
- 100+ валидациски правила (од правилник)
- 20+ код листи (procedure codes, document types, countries...)
- 168 страници правилник → ~500 semantic chunks

---

## 🚀 Следни чекори

1. **Одлучи за AI provider**:
   - OpenAI API (платена, најдобра квалитет)
   - Azure OpenAI (enterprise, compliance)
   - Локален модел (llama.cpp, Ollama - бесплатно, но побаво)

2. **Vector DB избор**:
   - pgvector (PostgreSQL extension) - најлесно ако веќе има Postgres
   - Azure AI Search - целосно управувана
   - ChromaDB - lightweight, open-source

3. **Priority**:
   - Фаза 1 (Data Loading) - **HIGHEST** - структурирани податоци се критични
   - Фаза 2 (Rule Engine) - **HIGH** - compliance е must-have
   - Фаза 3 (Vector Store) - **MEDIUM** - nice-to-have, AI assistant
   - Фаза 4 (UI) - **MEDIUM** - корисничко искуство

Што мислиш? Дали да почнеме со Фаза 1 - пребрување на податоците од Excel и креирање на миграции?
