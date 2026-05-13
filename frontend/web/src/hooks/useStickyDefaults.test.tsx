import React from 'react';
import { render, act } from '@testing-library/react';
import '@testing-library/jest-dom';
import { useStickyDefaults } from './useStickyDefaults';

type LineFixture = {
  currency?: string;
  uom?: string;
  countryOfOrigin?: string;
  tariffCode?: string;
  quantity?: number;
  lineTotal?: number;
};

/**
 * Helper component that exposes the hook's API to the test through a ref.
 * Lets tests read `defaults` and invoke `captureFrom` / `reset` synchronously
 * via act() without needing to drive a real form.
 */
type HarnessApi = {
  read: () => Partial<LineFixture>;
  capture: (line: LineFixture) => void;
  reset: () => void;
};

const Harness = React.forwardRef<
  HarnessApi,
  { scopeKey: string; initial: Partial<LineFixture>; stickyFields?: ReadonlyArray<keyof LineFixture> }
>(({ scopeKey, initial, stickyFields }, ref) => {
  const { defaults, captureFrom, reset } = useStickyDefaults<LineFixture>(scopeKey, initial, stickyFields);
  React.useImperativeHandle(ref, () => ({
    read: () => defaults,
    capture: (line) => captureFrom(line),
    reset,
  }), [defaults, captureFrom, reset]);
  return <div data-testid="harness" />;
});

describe('useStickyDefaults', () => {
  it('returns initial defaults on first call', () => {
    const ref = React.createRef<HarnessApi>();
    render(<Harness ref={ref} scopeKey="t1" initial={{ currency: 'EUR' }} />);
    expect(ref.current?.read()).toEqual({ currency: 'EUR' });
  });

  it('captures defined values from saved line for next line prefill', () => {
    const ref = React.createRef<HarnessApi>();
    render(<Harness ref={ref} scopeKey="t2" initial={{ currency: 'EUR' }} />);
    act(() => {
      ref.current!.capture({ currency: 'USD', uom: 'KGM', countryOfOrigin: 'DE' });
    });
    expect(ref.current?.read()).toEqual({ currency: 'USD', uom: 'KGM', countryOfOrigin: 'DE' });
  });

  it('skips undefined / null / empty-string values when capturing', () => {
    const ref = React.createRef<HarnessApi>();
    render(<Harness ref={ref} scopeKey="t3" initial={{ currency: 'EUR' }} />);
    act(() => {
      ref.current!.capture({
        currency: undefined,
        uom: '',
        countryOfOrigin: 'DE',
        tariffCode: '5210',
      } as LineFixture);
    });
    // currency stays EUR (undefined ignored); uom stays unset (empty ignored).
    expect(ref.current?.read()).toEqual({
      currency: 'EUR',
      countryOfOrigin: 'DE',
      tariffCode: '5210',
    });
  });

  it('does not capture quantity / lineTotal when stickyFields whitelist excludes them', () => {
    const ref = React.createRef<HarnessApi>();
    render(
      <Harness
        ref={ref}
        scopeKey="t4"
        initial={{ currency: 'EUR' }}
        stickyFields={['currency', 'uom', 'countryOfOrigin', 'tariffCode']}
      />,
    );
    act(() => {
      ref.current!.capture({ currency: 'USD', uom: 'KGM', quantity: 100, lineTotal: 250.5 });
    });
    const out = ref.current!.read();
    expect(out.currency).toBe('USD');
    expect(out.uom).toBe('KGM');
    expect(out).not.toHaveProperty('quantity');
    expect(out).not.toHaveProperty('lineTotal');
  });

  it('reset returns to initial defaults', () => {
    const ref = React.createRef<HarnessApi>();
    render(<Harness ref={ref} scopeKey="t5" initial={{ currency: 'EUR' }} />);
    act(() => {
      ref.current!.capture({ currency: 'USD', uom: 'KGM' });
    });
    expect(ref.current?.read().currency).toBe('USD');
    act(() => {
      ref.current!.reset();
    });
    expect(ref.current?.read()).toEqual({ currency: 'EUR' });
  });

  it('two separate hook instances with different scopeKeys do not share state', () => {
    const refA = React.createRef<HarnessApi>();
    const refB = React.createRef<HarnessApi>();
    render(
      <>
        <Harness ref={refA} scopeKey="scope-A" initial={{ currency: 'EUR' }} />
        <Harness ref={refB} scopeKey="scope-B" initial={{ currency: 'USD' }} />
      </>,
    );
    act(() => {
      refA.current!.capture({ currency: 'CHF', uom: 'MTR' });
    });
    expect(refA.current?.read().currency).toBe('CHF');
    expect(refA.current?.read().uom).toBe('MTR');
    // B unaffected.
    expect(refB.current?.read()).toEqual({ currency: 'USD' });
  });

  it('captures incrementally: line 2 builds on line 1 defaults', () => {
    const ref = React.createRef<HarnessApi>();
    render(<Harness ref={ref} scopeKey="t7" initial={{ currency: 'EUR' }} />);
    act(() => {
      ref.current!.capture({ uom: 'KGM' });
    });
    act(() => {
      ref.current!.capture({ countryOfOrigin: 'DE' });
    });
    expect(ref.current?.read()).toEqual({ currency: 'EUR', uom: 'KGM', countryOfOrigin: 'DE' });
  });
});
