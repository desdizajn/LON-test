import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Box,
  Paper,
  Typography,
  TextField,
  Button,
  Stack,
  Chip,
  LinearProgress,
  MenuItem,
  Select,
  InputLabel,
  FormControl,
  Slider,
} from '@mui/material';
import SearchIcon from '@mui/icons-material/Search';
import { api } from '../../services/api';
import { showError } from '../../utils/toast';

/**
 * P6.38 — Semantic search over Knowledge Base (Правилник + SAD guidance).
 * Distinct from KnowledgeBaseChat which is the conversational RAG flow;
 * this page surfaces raw chunk hits with similarity scores for operators
 * that want to "find the rule" rather than "ask the assistant".
 *
 * Wires to POST /api/KnowledgeBase/search (fixed in P6.42).
 */

interface ChunkHit {
  chunkId: string;
  documentId: string;
  content: string;
  chunkTitle: string | null;
  documentTitle: string | null;
  documentType: string | null;
  reference: string | null;
  similarityScore: number;
}

const DOC_TYPES = ['', 'Правилник', 'SADка Упатство'];

const KnowledgeBaseSearch: React.FC = () => {
  const { t } = useTranslation();
  const [query, setQuery] = useState('');
  const [topK, setTopK] = useState(5);
  const [docType, setDocType] = useState<string>('');
  const [minSim, setMinSim] = useState(0.6);
  const [results, setResults] = useState<ChunkHit[] | null>(null);
  const [loading, setLoading] = useState(false);

  const runSearch = async () => {
    const q = query.trim();
    if (!q) return;
    setLoading(true);
    try {
      // Direct api.post because the knowledgeBaseApi.search helper only
      // accepts (query, topK) — we need minSimilarity + documentType too.
      const resp = await api.post('/KnowledgeBase/search', {
        query: q,
        topK,
        minSimilarity: minSim,
        documentType: docType || null,
      });
      setResults(resp.data as ChunkHit[]);
    } catch (err: any) {
      showError(err?.response?.data ?? t('itemAttributes.loadError'));
      setResults([]);
    } finally {
      setLoading(false);
    }
  };

  return (
    <Box sx={{ p: 3 }}>
      <Typography variant="h4" gutterBottom>
        🔍 {t('kbSearch.title')}
      </Typography>
      <Typography variant="body2" color="text.secondary" paragraph>
        {t('kbSearch.subtitle')}
      </Typography>

      <Paper sx={{ p: 2, mb: 2 }}>
        <Stack spacing={2}>
          <TextField
            fullWidth
            placeholder={t('kbSearch.queryPlaceholder')}
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter' && !loading) runSearch();
            }}
            InputProps={{
              startAdornment: <SearchIcon sx={{ mr: 1, color: 'text.secondary' }} />,
            }}
          />

          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} alignItems="center">
            <TextField
              label={t('kbSearch.topK')}
              type="number"
              value={topK}
              onChange={(e) => setTopK(Math.max(1, Math.min(20, parseInt(e.target.value) || 5)))}
              sx={{ width: 140 }}
              inputProps={{ min: 1, max: 20 }}
            />

            <FormControl sx={{ minWidth: 200 }}>
              <InputLabel>{t('kbSearch.documentType')}</InputLabel>
              <Select
                value={docType}
                label={t('kbSearch.documentType')}
                onChange={(e) => setDocType(e.target.value)}
              >
                <MenuItem value="">{t('kbSearch.allTypes')}</MenuItem>
                {DOC_TYPES.filter((t) => t).map((dt) => (
                  <MenuItem key={dt} value={dt}>
                    {dt}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>

            <Box sx={{ flex: 1, minWidth: 200 }}>
              <Typography variant="caption">
                {t('kbSearch.minSimilarity')}: {minSim.toFixed(2)}
              </Typography>
              <Slider
                value={minSim}
                onChange={(_, v) => setMinSim(v as number)}
                min={0}
                max={1}
                step={0.05}
                valueLabelDisplay="auto"
              />
            </Box>

            <Button
              variant="contained"
              onClick={runSearch}
              disabled={!query.trim() || loading}
              startIcon={<SearchIcon />}
            >
              {loading ? t('kbSearch.searching') : t('kbSearch.search')}
            </Button>
          </Stack>
        </Stack>
      </Paper>

      {loading && <LinearProgress sx={{ mb: 2 }} />}

      {results !== null && !loading && (
        <>
          <Typography variant="subtitle2" sx={{ mb: 1 }}>
            {t('kbSearch.resultsCount', { count: results.length })}
          </Typography>

          {results.length === 0 ? (
            <Paper sx={{ p: 2, textAlign: 'center', color: 'text.secondary' }}>
              {t('kbSearch.noResults')}
            </Paper>
          ) : (
            <Stack spacing={2}>
              {results.map((r) => (
                <Paper key={r.chunkId} sx={{ p: 2 }}>
                  <Stack direction="row" spacing={1} sx={{ mb: 1 }} flexWrap="wrap">
                    {r.documentTitle && (
                      <Chip size="small" label={r.documentTitle} color="primary" variant="outlined" />
                    )}
                    {r.documentType && <Chip size="small" label={r.documentType} />}
                    {r.reference && (
                      <Chip
                        size="small"
                        label={`${t('kbSearch.reference')}: ${r.reference}`}
                        variant="outlined"
                      />
                    )}
                    <Chip
                      size="small"
                      label={`${t('kbSearch.similarity')}: ${(r.similarityScore * 100).toFixed(1)}%`}
                      color={r.similarityScore >= 0.8 ? 'success' : 'default'}
                    />
                  </Stack>
                  <Typography variant="body2" sx={{ whiteSpace: 'pre-wrap' }}>
                    {r.content}
                  </Typography>
                </Paper>
              ))}
            </Stack>
          )}
        </>
      )}

      {results === null && !loading && (
        <Typography variant="body2" color="text.secondary">
          {t('kbSearch.noQuery')}
        </Typography>
      )}
    </Box>
  );
};

export default KnowledgeBaseSearch;
