export interface UserProfile {
  fullName: string;
  email: string;
  phone: string;
  linkedInUrl: string;
  gitHubUrl: string;
  websiteUrl: string;
  address: string;
  addresses?: string[];
  role?: string;
  footerText?: string;
}

export interface AppSettings {
  openRouterApiKey: string;
  openRouterModelChain: string[];
  groqApiKey: string;
  groqModel: string;
  ollamaBaseUrl: string;
  ollamaModel: string;
  providerPriority: string[];
  theme: string;
  exportDirectory: string;
  profile: UserProfile;
}

export interface McpConfig {
  isEnabled: boolean;
  mcpBaseUrl: string;
  mcpApiKey: string;
}

export interface CoverLetterRequest {
  mode: string; // 'existing' | 'generate'
  rawRecipientInfo?: string;
  jobDescription?: string;
  companyName: string;
  position: string;
  contactPerson?: string;
  department?: string;
  companyLocation: string;
  language: string;
  selectedTemplate: string; // 'template_1' | 'template_2'
  headerAddress?: string;
  coverLetterBody: string;
}

export interface CoverLetterRecord {
  id: number;
  companyName: string;
  position: string;
  contactPerson?: string;
  department?: string;
  companyLocation: string;
  language: string;
  contentBody: string;
  createdAt: string;
  pdfPath: string;
  status: string;
  usedProvider: string;
  usedModel: string;
  selectedTemplate: string;
  headerAddress?: string;
  syncedToMcp: boolean;
}

export interface LlmResult {
  layout: {
    companyNameFormatted: string;
    positionFormatted: string;
    salutationLine: string;
    bodyParagraphs: string[];
    closingLine: string;
    signerName: string;
    companyLocation: string;
    contactPerson: string;
    department: string;
  };
  pdfBase64?: string;
  pdfPath?: string;
  wasFallback: boolean;
  warnings: string[];
  attemptLog: string[];
  syncedToMcp: boolean;
}

export interface ResumeRequest {
  summary: string;
  experience: string;
  education: string;
  skills: string;
  projects: string;
  language: string;
  selectedTemplate: string;
  headerAddress: string;
}

export interface ResumeResult {
  layout: {
    summaryFormatted: string;
    experienceFormatted: string;
    educationFormatted: string;
    skillsFormatted: string;
    projectsFormatted: string;
  };
  pdfBase64?: string;
  pdfPath?: string;
  wasFallback: boolean;
  warnings: string[];
  attemptLog: string[];
  usedProvider: string;
  usedModel: string;
}

export interface ResumeRecord {
  id: number;
  summary: string;
  experience: string;
  education: string;
  skills: string;
  projects: string;
  language: string;
  selectedTemplate: string;
  headerAddress: string;
  pdfPath: string;
  syncedToMcp: boolean;
  createdAt: string;
  usedProvider: string;
  usedModel: string;
}

