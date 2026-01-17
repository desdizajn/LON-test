import React, { useState, useEffect } from 'react';
import { knowledgeBaseApi } from '../../services/api';
import { KnowledgeBaseAnswer, DocumentChunk } from '../../types/knowledgeBase';
import { toast } from 'react-toastify';
import './KnowledgeBaseChat.css';

interface Message {
  id: number;
  type: 'user' | 'assistant';
  content: string;
  sources?: DocumentChunk[];
  timestamp: Date;
}

const KnowledgeBaseChat: React.FC = () => {
  const [messages, setMessages] = useState<Message[]>([
    {
      id: 0,
      type: 'assistant',
      content: 'Здраво! Јас сум вашиот асистент за царински прописи и процедури. Прашајте ме што сакате за SADка, МРД, гаранции, тарифни ознаки, или било што поврзано со царинење. 🚢',
      timestamp: new Date(),
    }
  ]);
  const [inputValue, setInputValue] = useState('');
  const [loading, setLoading] = useState(false);
  const [health, setHealth] = useState<any>(null);
  const [expandedSources, setExpandedSources] = useState<Set<number>>(new Set());

  useEffect(() => {
    checkHealth();
  }, []);

  const checkHealth = async () => {
    try {
      const res = await knowledgeBaseApi.getHealth();
      setHealth(res.data);
      if (!res.data.isHealthy) {
        toast.warning('Knowledge Base is not fully configured');
      }
    } catch (err) {
      toast.error('Failed to connect to Knowledge Base');
    }
  };

  const handleSend = async () => {
    if (!inputValue.trim()) return;

    const userMessage: Message = {
      id: messages.length,
      type: 'user',
      content: inputValue,
      timestamp: new Date(),
    };

    setMessages(prev => [...prev, userMessage]);
    setInputValue('');
    setLoading(true);

    try {
      const res = await knowledgeBaseApi.ask(inputValue);
      const answer: KnowledgeBaseAnswer = res.data;

      const assistantMessage: Message = {
        id: messages.length + 1,
        type: 'assistant',
        content: answer.answer,
        sources: answer.sources,
        timestamp: new Date(),
      };

      setMessages(prev => [...prev, assistantMessage]);
    } catch (err: any) {
      const errorMessage: Message = {
        id: messages.length + 1,
        type: 'assistant',
        content: 'Извинете, имаше грешка при обработката на вашето прашање. Обидете се повторно.',
        timestamp: new Date(),
      };
      setMessages(prev => [...prev, errorMessage]);
      toast.error(err.response?.data?.message || 'Failed to get answer');
    } finally {
      setLoading(false);
    }
  };

  const handleKeyPress = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSend();
    }
  };

  const toggleSources = (messageId: number) => {
    setExpandedSources(prev => {
      const newSet = new Set(prev);
      if (newSet.has(messageId)) {
        newSet.delete(messageId);
      } else {
        newSet.add(messageId);
      }
      return newSet;
    });
  };

  const copyToClipboard = (text: string) => {
    navigator.clipboard.writeText(text);
    toast.success('Copied to clipboard!');
  };

  const clearChat = () => {
    setMessages([{
      id: 0,
      type: 'assistant',
      content: 'Здраво! Јас сум вашиот асистент за царински прописи и процедури. Прашајте ме што сакате! 🚢',
      timestamp: new Date(),
    }]);
  };

  const suggestedQuestions = [
    'Што е Box 15a во SADката?',
    'Како се пресметува царина за облагородување?',
    'Кои документи се потребни за извоз?',
    'Објасни ми процедура 51 - Инвард Processing',
    'Кои се рокови за МРД?',
  ];

  return (
    <div className="kb-chat-container">
      <div className="kb-chat-header">
        <div>
          <h2>🧠 Knowledge Base Assistant</h2>
          <p>RAG-Powered Smart Help for Customs & Regulations</p>
        </div>
        <div className="kb-status">
          {health && (
            <span className={health.isHealthy ? 'status-indicator status-ok' : 'status-indicator status-error'}>
              {health.isHealthy ? '● Online' : '● Offline'}
            </span>
          )}
          <button className="btn btn-sm btn-secondary" onClick={clearChat}>
            Clear Chat
          </button>
        </div>
      </div>

      <div className="kb-chat-messages">
        {messages.map((message) => (
          <div key={message.id} className={`kb-message ${message.type}`}>
            <div className="kb-message-content">
              <div className="kb-message-text">{message.content}</div>
              
              {message.type === 'assistant' && (
                <div className="kb-message-actions">
                  <button
                    className="btn-icon"
                    onClick={() => copyToClipboard(message.content)}
                    title="Copy"
                  >
                    📋
                  </button>
                </div>
              )}
            </div>

            {message.sources && message.sources.length > 0 && (
              <div className="kb-sources">
                <button
                  className="kb-sources-toggle"
                  onClick={() => toggleSources(message.id)}
                >
                  📚 {message.sources.length} Sources
                  {expandedSources.has(message.id) ? ' ▼' : ' ▶'}
                </button>

                {expandedSources.has(message.id) && (
                  <div className="kb-sources-list">
                    {message.sources.map((source, idx) => (
                      <div key={idx} className="kb-source-item">
                        <div className="kb-source-header">
                          <strong>{source.documentTitle}</strong>
                          <span className="kb-relevance-score">
                            Score: {(source.relevanceScore * 100).toFixed(1)}%
                          </span>
                        </div>
                        <div className="kb-source-content">
                          {source.content.substring(0, 200)}...
                        </div>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            )}

            <div className="kb-message-timestamp">
              {message.timestamp.toLocaleTimeString()}
            </div>
          </div>
        ))}

        {loading && (
          <div className="kb-message assistant">
            <div className="kb-message-content">
              <div className="kb-typing-indicator">
                <span></span>
                <span></span>
                <span></span>
              </div>
            </div>
          </div>
        )}
      </div>

      {messages.length === 1 && (
        <div className="kb-suggestions">
          <p><strong>Try asking:</strong></p>
          <div className="kb-suggestions-grid">
            {suggestedQuestions.map((q, idx) => (
              <button
                key={idx}
                className="kb-suggestion-btn"
                onClick={() => setInputValue(q)}
              >
                {q}
              </button>
            ))}
          </div>
        </div>
      )}

      <div className="kb-chat-input">
        <textarea
          value={inputValue}
          onChange={(e) => setInputValue(e.target.value)}
          onKeyPress={handleKeyPress}
          placeholder="Прашајте за царински прописи, процедури, тарифи..."
          rows={2}
          disabled={loading}
        />
        <button
          className="btn btn-primary"
          onClick={handleSend}
          disabled={loading || !inputValue.trim()}
        >
          {loading ? 'Sending...' : 'Send'}
        </button>
      </div>
    </div>
  );
};

export default KnowledgeBaseChat;
