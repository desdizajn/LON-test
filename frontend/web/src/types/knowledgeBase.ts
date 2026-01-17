// Knowledge Base TypeScript Interfaces

export interface KnowledgeBaseQuestion {
  question: string;
  context?: string;
}

export interface KnowledgeBaseAnswer {
  answer: string;
  sources: DocumentChunk[];
  confidence?: number;
}

export interface DocumentChunk {
  id: string;
  documentTitle: string;
  content: string;
  chunkIndex: number;
  relevanceScore: number;
  metadata?: any;
}

export interface SearchRequest {
  query: string;
  topK?: number;
}

export interface SearchResult {
  results: DocumentChunk[];
  totalFound: number;
}

export interface ExplainRequest {
  field: string;
  context?: string;
}

export interface ExplainResponse {
  explanation: string;
  relatedFields?: string[];
  examples?: string[];
  regulations?: string[];
}

export interface KnowledgeBaseStats {
  totalDocuments: number;
  totalChunks: number;
  lastUpdated: string;
  documentTypes: {[key: string]: number};
  averageChunkSize: number;
}

export interface KnowledgeBaseHealth {
  isHealthy: boolean;
  openAIConfigured: boolean;
  embeddingModelStatus: string;
  chatModelStatus: string;
  vectorStoreStatus: string;
  totalDocuments: number;
  totalChunks: number;
  message?: string;
}
