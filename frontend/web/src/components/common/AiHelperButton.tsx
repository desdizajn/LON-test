import React, { useEffect, useMemo, useState } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  Alert,
  Badge,
  Box,
  Button,
  CircularProgress,
  Divider,
  Drawer,
  Fab,
  IconButton,
  List,
  ListItem,
  ListItemText,
  Stack,
  Tab,
  Tabs,
  TextField,
  Tooltip,
  Typography,
} from '@mui/material';
import AutoAwesomeIcon from '@mui/icons-material/AutoAwesome';
import CloseIcon from '@mui/icons-material/Close';
import SendIcon from '@mui/icons-material/Send';
import { aiApi, AiAskResponse, AiRecommendation } from '../../services/api';
import { useAiHelperContext } from '../../contexts/AiHelperContext';

const HUB_ACTION_TO_ROUTE: Record<string, (clientOrderId: string) => string> = {
  // Most hub actions open dialogs on the hub itself — the action link is just
  // the hub URL with the launcher visible. Razdolzuvanje is the only one that
  // navigates away.
  'orders.actions.razdolzuvanje': (id) => `/orders/${id}/razdolzuvanje`,
};

interface ChatTurn {
  role: 'user' | 'assistant';
  content: string;
}

const AiHelperButton: React.FC = () => {
  const { t } = useTranslation();
  const { entityType, entityId } = useAiHelperContext();
  const location = useLocation();
  const navigate = useNavigate();

  const [open, setOpen] = useState(false);
  const [tab, setTab] = useState<'recs' | 'ask'>('recs');
  const [recs, setRecs] = useState<AiRecommendation[]>([]);
  const [loadingRecs, setLoadingRecs] = useState(false);
  const [recsError, setRecsError] = useState<string | null>(null);

  const [question, setQuestion] = useState('');
  const [chat, setChat] = useState<ChatTurn[]>([]);
  const [asking, setAsking] = useState(false);

  // Hide the FAB on login + setup screens.
  const hide = useMemo(
    () => location.pathname.startsWith('/login') || location.pathname.startsWith('/setup'),
    [location.pathname],
  );

  const fetchRecs = async () => {
    if (!entityType || !entityId) {
      setRecs([]);
      return;
    }
    setLoadingRecs(true);
    setRecsError(null);
    try {
      const resp = await aiApi.getRecommendations(entityType, entityId);
      setRecs(resp.data || []);
    } catch (err) {
      // eslint-disable-next-line no-console
      console.warn('[AiHelper] failed to load recommendations', err);
      setRecsError(t('ai.errors.recsFailed') as string);
      setRecs([]);
    } finally {
      setLoadingRecs(false);
    }
  };

  useEffect(() => {
    if (open && tab === 'recs') {
      fetchRecs();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, tab, entityType, entityId]);

  const handleAct = async (rec: AiRecommendation) => {
    try {
      await aiApi.markActed(rec.id);
    } catch {
      /* analytics-only — surface failures are non-fatal */
    }
    setOpen(false);

    if (!rec.actionLink) return;
    // Deep-link style: navigate to a raw route.
    if (rec.actionLink.startsWith('/')) {
      navigate(rec.actionLink);
      return;
    }
    // Hub-action style: try the mapped router function.
    if (entityType === 'ClientOrder' && entityId) {
      const router = HUB_ACTION_TO_ROUTE[rec.actionLink];
      if (router) {
        navigate(router(entityId));
        return;
      }
      // Default: navigate to the hub and surface the action key for the
      // hub action launcher to focus / open. The hub reads ?action= from
      // the query string.
      navigate(`/orders/${entityId}?action=${encodeURIComponent(rec.actionLink)}`);
    }
  };

  const handleDismiss = async (rec: AiRecommendation) => {
    try {
      await aiApi.markDismissed(rec.id);
    } catch {
      /* analytics-only */
    }
    setRecs((prev) => prev.filter((r) => r.id !== rec.id));
  };

  const handleAsk = async () => {
    const q = question.trim();
    if (!q || asking) return;
    setAsking(true);
    setChat((prev) => [...prev, { role: 'user', content: q }]);
    setQuestion('');
    try {
      const resp = await aiApi.ask(q);
      const answer = (resp.data as AiAskResponse).answer ?? '';
      setChat((prev) => [...prev, { role: 'assistant', content: answer || (t('ai.errors.emptyAnswer') as string) }]);
    } catch (err) {
      setChat((prev) => [
        ...prev,
        { role: 'assistant', content: t('ai.errors.askFailed') as string },
      ]);
    } finally {
      setAsking(false);
    }
  };

  if (hide) return null;

  return (
    <>
      <Tooltip title={t('ai.fab.tooltip') as string}>
        <Fab
          color="primary"
          aria-label="ai-helper"
          onClick={() => setOpen(true)}
          sx={{ position: 'fixed', bottom: 24, right: 24, zIndex: (theme) => theme.zIndex.drawer + 2 }}
          data-testid="ai-helper-fab"
        >
          <Badge color="warning" variant="dot" invisible={recs.length === 0}>
            <AutoAwesomeIcon />
          </Badge>
        </Fab>
      </Tooltip>

      <Drawer
        anchor="right"
        open={open}
        onClose={() => setOpen(false)}
        PaperProps={{ sx: { width: { xs: '100%', sm: 420 }, maxWidth: '100%' } }}
      >
        <Stack direction="row" alignItems="center" justifyContent="space-between" sx={{ p: 2, pb: 1 }}>
          <Typography variant="h6" sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
            <AutoAwesomeIcon fontSize="small" /> {t('ai.drawer.title')}
          </Typography>
          <IconButton onClick={() => setOpen(false)} aria-label="close">
            <CloseIcon />
          </IconButton>
        </Stack>
        <Tabs value={tab} onChange={(_, v) => setTab(v)} variant="fullWidth">
          <Tab value="recs" label={t('ai.tabs.recommendations') as string} />
          <Tab value="ask" label={t('ai.tabs.ask') as string} />
        </Tabs>
        <Divider />

        {tab === 'recs' && (
          <Box sx={{ p: 2, overflow: 'auto' }}>
            {!entityType || !entityId ? (
              <Alert severity="info">{t('ai.recs.noContext')}</Alert>
            ) : loadingRecs ? (
              <Stack direction="row" alignItems="center" spacing={1}>
                <CircularProgress size={18} />
                <Typography>{t('ai.recs.loading')}</Typography>
              </Stack>
            ) : recsError ? (
              <Alert severity="error">{recsError}</Alert>
            ) : recs.length === 0 ? (
              <Alert severity="success">{t('ai.recs.empty')}</Alert>
            ) : (
              <Stack spacing={2}>
                {recs.map((r) => (
                  <Alert
                    key={r.id}
                    severity={r.severity === 'warning' ? 'warning' : r.severity === 'success' ? 'success' : 'info'}
                    action={
                      <Stack direction="row" spacing={1}>
                        {r.actionLink && (
                          <Button color="inherit" size="small" onClick={() => handleAct(r)}>
                            {r.actionLabel || (t('ai.recs.openAction') as string)}
                          </Button>
                        )}
                        <Button color="inherit" size="small" onClick={() => handleDismiss(r)}>
                          {t('ai.recs.dismiss')}
                        </Button>
                      </Stack>
                    }
                  >
                    <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>
                      {r.title}
                    </Typography>
                    <Typography variant="body2" sx={{ mt: 0.5, whiteSpace: 'pre-wrap' }}>
                      {r.body}
                    </Typography>
                  </Alert>
                ))}
              </Stack>
            )}
            <Box sx={{ mt: 2 }}>
              <Button size="small" onClick={fetchRecs} disabled={loadingRecs || !entityType}>
                {t('ai.recs.refresh')}
              </Button>
            </Box>
          </Box>
        )}

        {tab === 'ask' && (
          <Box sx={{ p: 2, display: 'flex', flexDirection: 'column', flex: 1, minHeight: 0 }}>
            <Box sx={{ flex: 1, overflow: 'auto', mb: 1 }}>
              {chat.length === 0 ? (
                <Alert severity="info">{t('ai.ask.hint')}</Alert>
              ) : (
                <List dense>
                  {chat.map((turn, idx) => (
                    <ListItem key={idx} alignItems="flex-start" sx={{ flexDirection: 'column' }}>
                      <Typography
                        variant="caption"
                        sx={{ fontWeight: 600, color: turn.role === 'user' ? 'primary.main' : 'text.secondary' }}
                      >
                        {turn.role === 'user' ? t('ai.ask.you') : t('ai.ask.assistant')}
                      </Typography>
                      <ListItemText primary={<Typography sx={{ whiteSpace: 'pre-wrap' }}>{turn.content}</Typography>} />
                    </ListItem>
                  ))}
                </List>
              )}
            </Box>
            <Stack direction="row" spacing={1}>
              <TextField
                fullWidth
                size="small"
                placeholder={t('ai.ask.placeholder') as string}
                value={question}
                onChange={(e) => setQuestion(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === 'Enter' && !e.shiftKey) {
                    e.preventDefault();
                    handleAsk();
                  }
                }}
                disabled={asking}
              />
              <IconButton color="primary" onClick={handleAsk} disabled={asking || !question.trim()}>
                {asking ? <CircularProgress size={20} /> : <SendIcon />}
              </IconButton>
            </Stack>
          </Box>
        )}
      </Drawer>
    </>
  );
};

export default AiHelperButton;
