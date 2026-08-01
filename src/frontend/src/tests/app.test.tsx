import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import React from 'react';
import App from '../App';

describe('App', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async () => {
      return {
        ok: true,
        status: 200,
        text: async () => '',
        json: async () => []
      } as any;
    }));
  });

  it('renders the page title', () => {
    render(<App />);
    expect(screen.getByText(/Real-Time Currency Conversion/i)).toBeInTheDocument();
  });

  it('allows typing an amount', async () => {
    const user = userEvent.setup();
    render(<App />);
    const amount = screen.getByLabelText(/Amount/i);
    await user.clear(amount);
    await user.type(amount, '12.5');
    expect((amount as HTMLInputElement).value).toBe('12.5');
  });
});
