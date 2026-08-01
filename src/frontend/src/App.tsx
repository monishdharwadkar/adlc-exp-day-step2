import React, { useEffect, useMemo, useState } from 'react';
import { api } from './api/client';

type ConversionPreview = {
  auditId: string;
  fromCurrency: string;
  toCurrency: string;
  inputAmount: number;
  rate: number;
  convertedAmount: number;
  backendExecutionTimestampUtc: string;
  providerDate: string | null;
};

type ConversionAudit = ConversionPreview;

const supportedCurrencies = ['USD', 'EUR', 'GBP', 'JPY', 'CAD', 'AUD', 'CHF', 'CNY', 'INR', 'BRL'];

export default function App() {
  const [fromCurrency, setFromCurrency] = useState('USD');
  const [toCurrency, setToCurrency] = useState('EUR');
  const [amountText, setAmountText] = useState('100');

  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<ConversionPreview | null>(null);
  const [auditTrail, setAuditTrail] = useState<ConversionAudit[]>([]);
  const [detailsId, setDetailsId] = useState<string | null>(null);

  const details = useMemo(() => {
    if (!detailsId) return null;
    return auditTrail.find((x) => x.auditId === detailsId) ?? null;
  }, [auditTrail, detailsId]);

  async function loadAuditTrail() {
    const items = await api.listConversions({ limit: 25 });
    setAuditTrail(items);
  }

  useEffect(() => {
    void loadAuditTrail();
  }, []);

  async function onConvert() {
    setError(null);
    setResult(null);

    const amount = Number(amountText);
    if (!Number.isFinite(amount) || amount <= 0) {
      setError('Enter a positive numeric amount.');
      return;
    }

    setBusy(true);
    try {
      const preview = await api.convert({
        fromCurrency,
        toCurrency,
        amount
      });
      setResult(preview);
      await loadAuditTrail();
      setDetailsId(preview.auditId);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Conversion failed.');
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="page">
      <div className="header">
        <div>
          <div className="title">Real-Time Currency Conversion & Audit Trail</div>
          <div className="subtitle">Instant converted amount, with reconstructable backend execution timestamps.</div>
        </div>
      </div>

      <div className="grid">
        <div className="card">
          <div className="cardBody">
            <div className="form">
              <div className="row">
                <div>
                  <label>From currency</label>
                  <select value={fromCurrency} onChange={(e) => setFromCurrency(e.target.value)}>
                    {supportedCurrencies.map((c) => (
                      <option key={c} value={c}>
                        {c}
                      </option>
                    ))}
                  </select>
                </div>
                <div>
                  <label>To currency</label>
                  <select value={toCurrency} onChange={(e) => setToCurrency(e.target.value)}>
                    {supportedCurrencies.map((c) => (
                      <option key={c} value={c}>
                        {c}
                      </option>
                    ))}
                  </select>
                </div>
              </div>

              <div>
                <label htmlFor="amount-input">Amount</label>
                <input
                  id="amount-input"
                  value={amountText}
                  onChange={(e) => setAmountText(e.target.value)}
                  inputMode="decimal"
                />
              </div>

              <button onClick={() => void onConvert()} disabled={busy}>
                {busy ? 'Converting…' : 'Convert'}
              </button>

              {error ? <div className="error">{error}</div> : null}
              {result ? (
                <div className="success">
                  <div>
                    <span className="mono">{result.inputAmount}</span> {result.fromCurrency} ={' '}
                    <span className="mono">{result.convertedAmount}</span> {result.toCurrency}
                  </div>
                  <div style={{ marginTop: 6 }}>
                    Rate: <span className="mono">{result.rate}</span>
                  </div>
                </div>
              ) : null}
            </div>
          </div>
        </div>

        <div className="card">
          <div className="cardBody">
            <div style={{ fontWeight: 700, marginBottom: 10 }}>Audit trail (latest)</div>

            {auditTrail.length === 0 ? (
              <div style={{ color: '#475569', fontSize: 13 }}>No conversions yet.</div>
            ) : (
              <table className="table">
                <thead>
                  <tr>
                    <th>Time (UTC)</th>
                    <th>Pair</th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  {auditTrail.map((x) => (
                    <tr key={x.auditId}>
                      <td className="mono" style={{ whiteSpace: 'nowrap' }}>
                        {x.backendExecutionTimestampUtc}
                      </td>
                      <td>
                        {x.fromCurrency}→{x.toCurrency}
                      </td>
                      <td>
                        <button
                          className="smallBtn"
                          onClick={() => setDetailsId(x.auditId)}
                          disabled={detailsId === x.auditId}
                        >
                          Details
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}

            {details ? (
              <div style={{ marginTop: 14 }}>
                <div style={{ fontWeight: 700, marginBottom: 10 }}>Audit details</div>
                <div className="kv">
                  <div className="kvRow">
                    <span>Audit ID</span>
                    <span className="mono">{details.auditId}</span>
                  </div>
                  <div className="kvRow">
                    <span>Backend execution timestamp (UTC)</span>
                    <span className="mono">{details.backendExecutionTimestampUtc}</span>
                  </div>
                  <div className="kvRow">
                    <span>Provider date marker</span>
                    <span className="mono">{details.providerDate ?? 'n/a'}</span>
                  </div>
                  <div className="kvRow">
                    <span>Rate</span>
                    <span className="mono">{details.rate}</span>
                  </div>
                  <div className="kvRow">
                    <span>Converted amount</span>
                    <span className="mono">
                      {details.convertedAmount} {details.toCurrency}
                    </span>
                  </div>
                </div>
              </div>
            ) : null}
          </div>
        </div>
      </div>
    </div>
  );
}
