import { Injectable } from '@angular/core';
import { Title, Meta } from '@angular/platform-browser';
import { Router, NavigationEnd } from '@angular/router';
import { filter } from 'rxjs/operators';

export interface SeoConfig {
  title: string;
  description: string;
  keywords: string;
  canonicalUrl?: string;
  ogType?: string;
}

@Injectable({
  providedIn: 'root'
})
export class SeoService {
  private defaultDomain = 'https://maxer-z.vercel.app';

  private routeSeoMap: { [key: string]: SeoConfig } = {
    '/home': {
      title: 'MaxerZ — Free AI Resume Builder, Cover Letter Writer & ATS Score Checker',
      description: 'Create ATS-friendly resumes, generate tailored AI cover letters, and score your CV against real job descriptions for free with MaxerZ. Boost your interview chances today!',
      keywords: 'AI resume builder, ATS resume checker, free cover letter generator, ATS friendly resume, resume optimizer, job application AI, hire me resume, ATS score report',
      ogType: 'website'
    },
    '/compose': {
      title: 'AI Cover Letter Writer — Free Tailored Job Cover Letter Generator | MaxerZ',
      description: 'Generate professional, tailored cover letters customized for any job description in seconds using AI. Stand out to recruiters and hiring managers.',
      keywords: 'AI cover letter generator, tailored cover letter writer, job application cover letter, free cover letter template, professional cover letter maker',
      ogType: 'article'
    },
    '/resume': {
      title: 'Free AI Resume Builder & Optimizer — ATS Friendly CV Creator | MaxerZ',
      description: 'Build and optimize your resume for ATS algorithms. Re-order key sections, add languages & proficiencies, and export clean multi-page PDFs.',
      keywords: 'AI resume builder, ATS resume optimizer, CV generator, ATS resume format, job resume creator, multi-page resume PDF',
      ogType: 'article'
    },
    '/ats': {
      title: 'Free ATS Resume Review & Scoring Engine — AI Resume Checker | MaxerZ',
      description: 'Scan your resume against any job description with our 7-point AI ATS engine. Get instant match scores, formatting risk alerts, and line-by-line rewrite suggestions.',
      keywords: 'ATS resume checker, ATS resume score, free ATS review, ATS scanner online, resume job description match, applicant tracking system optimizer',
      ogType: 'article'
    },
    '/merge': {
      title: 'Free Online PDF Merger — Combine up to 5 PDF Files Instantly | MaxerZ',
      description: 'Merge up to 5 PDF resumes, portfolio documents, and certificates into a single high-quality PDF. Drag and drop to reorder before merging.',
      keywords: 'free PDF merger, merge resume PDFs, combine PDF online, join PDF documents, PDF joiner free',
      ogType: 'website'
    },
    '/history': {
      title: 'Saved Resumes & Cover Letter History | MaxerZ',
      description: 'Access your saved AI cover letters, optimized resumes, and PDF document export history.',
      keywords: 'saved cover letters, resume history, MaxerZ document archive',
      ogType: 'website'
    },
    '/settings': {
      title: 'Application Configurations & API Keys | MaxerZ',
      description: 'Manage API keys, applicant user profile settings, and PDF templates.',
      keywords: 'MaxerZ settings, API key configuration, resume profile settings',
      ogType: 'website'
    }
  };

  constructor(
    private titleService: Title,
    private metaService: Meta,
    private router: Router
  ) {
    this.initAutoSeo();
  }

  private initAutoSeo() {
    this.router.events.pipe(
      filter(event => event instanceof NavigationEnd)
    ).subscribe((event: any) => {
      const url = event.urlAfterRedirects.split('?')[0];
      const config = this.routeSeoMap[url] || this.routeSeoMap['/home'];
      this.updateSeo(config, url);
    });
  }

  updateSeo(config: SeoConfig, currentPath: string = '') {
    // 1. Title Tag
    this.titleService.setTitle(config.title);

    // 2. Meta Tags
    this.metaService.updateTag({ name: 'description', content: config.description });
    this.metaService.updateTag({ name: 'keywords', content: config.keywords });
    this.metaService.updateTag({ name: 'robots', content: 'index, follow, max-image-preview:large, max-snippet:-1, max-video-preview:-1' });

    // 3. OpenGraph Tags
    const fullUrl = `${this.defaultDomain}${currentPath}`;
    this.metaService.updateTag({ property: 'og:title', content: config.title });
    this.metaService.updateTag({ property: 'og:description', content: config.description });
    this.metaService.updateTag({ property: 'og:url', content: fullUrl });
    this.metaService.updateTag({ property: 'og:type', content: config.ogType || 'website' });
    this.metaService.updateTag({ property: 'og:site_name', content: 'MaxerZ' });

    // 4. Twitter Card Tags
    this.metaService.updateTag({ name: 'twitter:card', content: 'summary_large_image' });
    this.metaService.updateTag({ name: 'twitter:title', content: config.title });
    this.metaService.updateTag({ name: 'twitter:description', content: config.description });

    // 5. Canonical Link
    this.updateCanonicalLink(fullUrl);
  }

  private updateCanonicalLink(url: string) {
    let link: HTMLLinkElement | null = document.querySelector("link[rel='canonical']");
    if (!link) {
      link = document.createElement('link');
      link.setAttribute('rel', 'canonical');
      document.head.appendChild(link);
    }
    link.setAttribute('href', url);
  }
}
