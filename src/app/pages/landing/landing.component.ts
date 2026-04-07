import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-landing',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './landing.component.html',
  styleUrl: './landing.component.css'
})
export class LandingComponent {
  steps = [
    {
      number: '01',
      title: 'Paste the job description',
      description: 'Copy in the full JD — skills, experience, requirements. The more detail, the better the ranking.'
    },
    {
      number: '02',
      title: 'Upload candidate CVs',
      description: 'Drop in PDF, DOCX, or text files. Upload as many as you need — we handle the rest.'
    },
    {
      number: '03',
      title: 'Get your ranked shortlist',
      description: 'Candidates sorted by match score. See exactly why each one ranked high or low — then approve or pass.'
    }
  ];

  features = [
    {
      icon: 'upload',
      title: 'Bulk CV Upload',
      description: 'PDF, DOCX, or plain text. Upload an entire pipeline in seconds. We parse and extract skills automatically.'
    },
    {
      icon: 'brain',
      title: 'Transparent Scoring',
      description: 'Every CV gets a score against your JD. No black box — see matched keywords and what\'s missing.'
    },
    {
      icon: 'list',
      title: 'Ranked Shortlist',
      description: 'Candidates sorted by fit score. One-click approve or reject. Focus only on people worth your time.'
    }
  ];

  stats = [
    { value: '60%', label: 'of recruiter time is screening' },
    { value: '~10s', label: 'to score 50 CVs' },
    { value: '3 steps', label: 'to your shortlist' }
  ];
}
