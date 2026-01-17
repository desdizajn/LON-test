import React, { useEffect, useState } from 'react';
import { wmsApi, masterDataApi } from '../../services/api';

const CycleCountAccuracy: React.FC = () => {
  const [cycleCounts, setCycleCounts] = useState<any[]>([]);
  const [employees, setEmployees] = useState<any[]>([]);
  const [locations, setLocations] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);

  // Filters
  const [dateFrom, setDateFrom] = useState(() => {
    const date = new Date();
    date.setMonth(date.getMonth() - 3); // Last 3 months
    return date.toISOString().split('T')[0];
  });
  const [dateTo, setDateTo] = useState(() => new Date().toISOString().split('T')[0]);
  const [selectedEmployee, setSelectedEmployee] = useState('');
  const [selectedLocation, setSelectedLocation] = useState('');

  useEffect(() => {
    loadData();
  }, []);

  const loadData = async () => {
    try {
      setLoading(true);
      const [countsRes, employeesRes, locationsRes] = await Promise.all([
        wmsApi.getCycleCounts(),
        masterDataApi.getEmployees(),
        masterDataApi.getLocations(),
      ]);
      setCycleCounts(countsRes.data);
      setEmployees(employeesRes.data);
      setLocations(locationsRes.data);
    } catch (err) {
      console.error('Failed to load cycle count data', err);
    } finally {
      setLoading(false);
    }
  };

  // Filter cycle counts
  const filteredCounts = cycleCounts.filter(cc => {
    const countDate = new Date(cc.countDate).toISOString().split('T')[0];
    if (countDate < dateFrom || countDate > dateTo) return false;
    if (selectedEmployee && cc.countedByUserId.toString() !== selectedEmployee) return false;
    if (selectedLocation && cc.locationId.toString() !== selectedLocation) return false;
    return true;
  });

  // Calculate accuracy for a cycle count
  const calculateAccuracy = (cycleCount: any) => {
    if (!cycleCount.lines || cycleCount.lines.length === 0) return 0;
    
    const accurateLines = cycleCount.lines.filter((line: any) => {
      const variance = Math.abs(line.countedQuantity - line.systemQuantity);
      const systemQty = line.systemQuantity || 1; // Avoid division by 0
      const variancePercent = (variance / systemQty) * 100;
      return variancePercent < 2; // Accurate if variance < 2%
    });

    return (accurateLines.length / cycleCount.lines.length) * 100;
  };

  // Overall metrics
  const totalCounts = filteredCounts.length;
  const completedCounts = filteredCounts.filter(cc => cc.status === 2).length; // Assuming 2 = Completed
  
  const accuracyMetrics = filteredCounts
    .filter(cc => cc.status === 2)
    .map(cc => ({
      ...cc,
      accuracy: calculateAccuracy(cc),
      totalVariance: cc.lines?.reduce((sum: number, line: any) => 
        sum + Math.abs(line.countedQuantity - line.systemQuantity), 0) || 0,
    }));

  const averageAccuracy = accuracyMetrics.length > 0
    ? accuracyMetrics.reduce((sum, cc) => sum + cc.accuracy, 0) / accuracyMetrics.length
    : 0;

  const accurateCounts = accuracyMetrics.filter(cc => cc.accuracy >= 98).length;
  const accuratePercentage = completedCounts > 0 ? (accurateCounts / completedCounts * 100).toFixed(1) : 0;

  const totalVarianceQty = accuracyMetrics.reduce((sum, cc) => sum + cc.totalVariance, 0);

  // Accuracy by Employee
  const employeeAccuracy = accuracyMetrics.reduce((acc: any, cc: any) => {
    const employeeId = cc.countedByUserId;
    if (!acc[employeeId]) {
      const emp = employees.find(e => e.id === employeeId);
      acc[employeeId] = {
        employeeId,
        employeeName: emp?.name || 'Unknown',
        counts: 0,
        totalAccuracy: 0,
      };
    }
    acc[employeeId].counts++;
    acc[employeeId].totalAccuracy += cc.accuracy;
    return acc;
  }, {});

  const employeeAccuracyList = Object.values(employeeAccuracy).map((emp: any) => ({
    ...emp,
    avgAccuracy: emp.counts > 0 ? (emp.totalAccuracy / emp.counts).toFixed(2) : 0,
  })).sort((a: any, b: any) => b.avgAccuracy - a.avgAccuracy);

  // Accuracy by Location
  const locationAccuracy = accuracyMetrics.reduce((acc: any, cc: any) => {
    const locationId = cc.locationId;
    if (!acc[locationId]) {
      const loc = locations.find(l => l.id === locationId);
      acc[locationId] = {
        locationId,
        locationName: loc?.name || 'Unknown',
        counts: 0,
        totalAccuracy: 0,
      };
    }
    acc[locationId].counts++;
    acc[locationId].totalAccuracy += cc.accuracy;
    return acc;
  }, {});

  const locationAccuracyList = Object.values(locationAccuracy).map((loc: any) => ({
    ...loc,
    avgAccuracy: loc.counts > 0 ? (loc.totalAccuracy / loc.counts).toFixed(2) : 0,
  })).sort((a: any, b: any) => b.avgAccuracy - a.avgAccuracy);

  // Monthly trend (last 3 months)
  const last3Months = Array.from({ length: 3 }, (_, i) => {
    const date = new Date();
    date.setMonth(date.getMonth() - (2 - i));
    return {
      month: date.toLocaleString('default', { month: 'short', year: 'numeric' }),
      key: `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}`,
    };
  });

  const monthlyTrend = last3Months.map(month => {
    const monthCounts = accuracyMetrics.filter(cc => {
      const countMonth = new Date(cc.countDate).toISOString().substring(0, 7);
      return countMonth === month.key;
    });
    const avgAccuracy = monthCounts.length > 0
      ? monthCounts.reduce((sum, cc) => sum + cc.accuracy, 0) / monthCounts.length
      : 0;
    return {
      month: month.month,
      counts: monthCounts.length,
      accuracy: avgAccuracy.toFixed(2),
    };
  });

  // Export to Excel
  const exportToExcel = () => {
    const headers = ['Count Date', 'Location', 'Counted By', 'Total Lines', 'Accurate Lines', 'Accuracy %', 'Total Variance Qty'];
    const rows = accuracyMetrics.map(cc => {
      const emp = employees.find(e => e.id === cc.countedByUserId);
      const loc = locations.find(l => l.id === cc.locationId);
      const accurateLines = cc.lines?.filter((line: any) => {
        const variance = Math.abs(line.countedQuantity - line.systemQuantity);
        const variancePercent = (variance / (line.systemQuantity || 1)) * 100;
        return variancePercent < 2;
      }).length || 0;
      return [
        new Date(cc.countDate).toLocaleDateString(),
        loc?.name || '',
        emp?.name || '',
        cc.lines?.length || 0,
        accurateLines,
        cc.accuracy.toFixed(2),
        cc.totalVariance.toFixed(2),
      ];
    });

    const csv = [headers, ...rows].map(row => row.join(',')).join('\n');
    const blob = new Blob([csv], { type: 'text/csv' });
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `cycle_count_accuracy_${new Date().toISOString().split('T')[0]}.csv`;
    a.click();
  };

  if (loading) return <div className="loading">Loading cycle count data...</div>;

  return (
    <div>
      <div className="header">
        <h2>🎯 Cycle Count Accuracy Report</h2>
        <button className="btn btn-primary" onClick={exportToExcel}>
          📊 Export to Excel
        </button>
      </div>

      {/* Alert */}
      <div className="alert alert-info" style={{ marginBottom: '20px' }}>
        <strong>ℹ️ Cycle Count Accuracy Analysis</strong><br />
        Target: ≥98% accuracy (variance {'<'}2% per line). Tracks counting performance by employee and location.
      </div>

      {/* Filters */}
      <div className="card" style={{ padding: '15px', marginBottom: '20px' }}>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '10px' }}>
          <div>
            <label>From Date:</label>
            <input
              type="date"
              className="form-control"
              value={dateFrom}
              onChange={(e) => setDateFrom(e.target.value)}
            />
          </div>
          <div>
            <label>To Date:</label>
            <input
              type="date"
              className="form-control"
              value={dateTo}
              onChange={(e) => setDateTo(e.target.value)}
            />
          </div>
          <div>
            <label>Employee:</label>
            <select
              className="form-control"
              value={selectedEmployee}
              onChange={(e) => setSelectedEmployee(e.target.value)}
            >
              <option value="">All Employees</option>
              {employees.map(emp => (
                <option key={emp.id} value={emp.id}>{emp.name}</option>
              ))}
            </select>
          </div>
          <div>
            <label>Location:</label>
            <select
              className="form-control"
              value={selectedLocation}
              onChange={(e) => setSelectedLocation(e.target.value)}
            >
              <option value="">All Locations</option>
              {locations.map(loc => (
                <option key={loc.id} value={loc.id}>{loc.name}</option>
              ))}
            </select>
          </div>
        </div>
      </div>

      {/* Summary Cards */}
      <div className="summary-cards" style={{ 
        display: 'grid', 
        gridTemplateColumns: 'repeat(4, 1fr)', 
        gap: '15px', 
        marginBottom: '30px' 
      }}>
        <div className="card" style={{ padding: '20px', textAlign: 'center' }}>
          <div style={{ fontSize: '12px', color: '#666', marginBottom: '5px' }}>Total Cycle Counts</div>
          <div style={{ fontSize: '32px', fontWeight: 'bold' }}>{completedCounts}</div>
        </div>
        <div className="card" style={{ padding: '20px', textAlign: 'center' }}>
          <div style={{ fontSize: '12px', color: '#666', marginBottom: '5px' }}>Average Accuracy</div>
          <div style={{ 
            fontSize: '32px', 
            fontWeight: 'bold',
            color: averageAccuracy >= 98 ? '#28a745' : averageAccuracy >= 95 ? '#ffc107' : '#dc3545'
          }}>
            {averageAccuracy.toFixed(2)}%
          </div>
          <div style={{ fontSize: '12px' }}>Target: ≥98%</div>
        </div>
        <div className="card" style={{ padding: '20px', textAlign: 'center' }}>
          <div style={{ fontSize: '12px', color: '#666', marginBottom: '5px' }}>Accurate Counts (≥98%)</div>
          <div style={{ fontSize: '32px', fontWeight: 'bold', color: '#28a745' }}>{accurateCounts}</div>
          <div style={{ fontSize: '12px' }}>({accuratePercentage}% of total)</div>
        </div>
        <div className="card" style={{ padding: '20px', textAlign: 'center' }}>
          <div style={{ fontSize: '12px', color: '#666', marginBottom: '5px' }}>Total Variance Qty</div>
          <div style={{ fontSize: '32px', fontWeight: 'bold', color: '#dc3545' }}>{totalVarianceQty.toFixed(2)}</div>
        </div>
      </div>

      {/* Monthly Trend */}
      <div className="card" style={{ padding: '15px', marginBottom: '30px' }}>
        <h5>📈 Monthly Trend (Last 3 Months)</h5>
        <div className="table-container" style={{ marginTop: '15px' }}>
          <table>
            <thead>
              <tr>
                <th>Month</th>
                <th>Cycle Counts</th>
                <th>Avg Accuracy %</th>
                <th>Performance</th>
              </tr>
            </thead>
            <tbody>
              {monthlyTrend.map((month, idx) => (
                <tr key={idx}>
                  <td><strong>{month.month}</strong></td>
                  <td>{month.counts}</td>
                  <td>
                    <strong className={
                      parseFloat(month.accuracy) >= 98 ? 'text-success' : 
                      parseFloat(month.accuracy) >= 95 ? 'text-warning' : 
                      'text-danger'
                    }>
                      {month.accuracy}%
                    </strong>
                  </td>
                  <td>
                    {parseFloat(month.accuracy) >= 98 ? (
                      <span className="badge badge-success">✅ Excellent</span>
                    ) : parseFloat(month.accuracy) >= 95 ? (
                      <span className="badge badge-warning">⚠️ Good</span>
                    ) : (
                      <span className="badge badge-danger">❌ Needs Improvement</span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {/* Accuracy by Employee & Location */}
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '20px', marginBottom: '30px' }}>
        {/* By Employee */}
        <div className="card" style={{ padding: '15px' }}>
          <h5>👤 Accuracy by Employee</h5>
          <div className="table-container" style={{ marginTop: '15px' }}>
            <table>
              <thead>
                <tr>
                  <th>Employee</th>
                  <th>Counts</th>
                  <th>Avg Accuracy %</th>
                  <th>Rating</th>
                </tr>
              </thead>
              <tbody>
                {employeeAccuracyList.map((emp: any) => (
                  <tr key={emp.employeeId}>
                    <td><strong>{emp.employeeName}</strong></td>
                    <td>{emp.counts}</td>
                    <td>
                      <strong className={
                        emp.avgAccuracy >= 98 ? 'text-success' : 
                        emp.avgAccuracy >= 95 ? 'text-warning' : 
                        'text-danger'
                      }>
                        {emp.avgAccuracy}%
                      </strong>
                    </td>
                    <td>
                      {emp.avgAccuracy >= 98 ? '⭐⭐⭐' : emp.avgAccuracy >= 95 ? '⭐⭐' : '⭐'}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>

        {/* By Location */}
        <div className="card" style={{ padding: '15px' }}>
          <h5>📍 Accuracy by Location</h5>
          <div className="table-container" style={{ marginTop: '15px' }}>
            <table>
              <thead>
                <tr>
                  <th>Location</th>
                  <th>Counts</th>
                  <th>Avg Accuracy %</th>
                  <th>Status</th>
                </tr>
              </thead>
              <tbody>
                {locationAccuracyList.map((loc: any) => (
                  <tr key={loc.locationId}>
                    <td><strong>{loc.locationName}</strong></td>
                    <td>{loc.counts}</td>
                    <td>
                      <strong className={
                        loc.avgAccuracy >= 98 ? 'text-success' : 
                        loc.avgAccuracy >= 95 ? 'text-warning' : 
                        'text-danger'
                      }>
                        {loc.avgAccuracy}%
                      </strong>
                    </td>
                    <td>
                      {loc.avgAccuracy >= 98 ? '✅' : loc.avgAccuracy >= 95 ? '⚠️' : '❌'}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      </div>

      {/* All Cycle Counts Detail */}
      <div className="card" style={{ padding: '15px' }}>
        <h5>📋 All Cycle Counts Detail</h5>
        <div className="table-container" style={{ marginTop: '15px' }}>
          <table>
            <thead>
              <tr>
                <th>Count Date</th>
                <th>Location</th>
                <th>Counted By</th>
                <th>Lines</th>
                <th>Accurate Lines</th>
                <th>Accuracy %</th>
                <th>Variance Qty</th>
                <th>Rating</th>
              </tr>
            </thead>
            <tbody>
              {accuracyMetrics
                .sort((a, b) => new Date(b.countDate).getTime() - new Date(a.countDate).getTime())
                .map(cc => {
                  const emp = employees.find(e => e.id === cc.countedByUserId);
                  const loc = locations.find(l => l.id === cc.locationId);
                  const accurateLines = cc.lines?.filter((line: any) => {
                    const variance = Math.abs(line.countedQuantity - line.systemQuantity);
                    const variancePercent = (variance / (line.systemQuantity || 1)) * 100;
                    return variancePercent < 2;
                  }).length || 0;

                  return (
                    <tr key={cc.id} style={{
                      backgroundColor: cc.accuracy >= 98 ? '#d4edda' : cc.accuracy >= 95 ? '#fff3cd' : '#f8d7da'
                    }}>
                      <td>{new Date(cc.countDate).toLocaleDateString()}</td>
                      <td>{loc?.name || 'Unknown'}</td>
                      <td>{emp?.name || 'Unknown'}</td>
                      <td>{cc.lines?.length || 0}</td>
                      <td>{accurateLines}</td>
                      <td>
                        <strong className={
                          cc.accuracy >= 98 ? 'text-success' : 
                          cc.accuracy >= 95 ? 'text-warning' : 
                          'text-danger'
                        }>
                          {cc.accuracy.toFixed(2)}%
                        </strong>
                      </td>
                      <td>{cc.totalVariance.toFixed(2)}</td>
                      <td>
                        {cc.accuracy >= 98 ? (
                          <span className="badge badge-success">✅ Excellent</span>
                        ) : cc.accuracy >= 95 ? (
                          <span className="badge badge-warning">⚠️ Good</span>
                        ) : (
                          <span className="badge badge-danger">❌ Review</span>
                        )}
                      </td>
                    </tr>
                  );
                })}
            </tbody>
          </table>
        </div>
      </div>

      {/* Recommendations */}
      <div className="card" style={{ padding: '15px', marginTop: '20px', background: '#f8f9fa' }}>
        <h5>💡 Recommendations</h5>
        <ul style={{ marginTop: '10px', marginBottom: 0 }}>
          <li>Target accuracy: ≥98% (variance {'<'}2% per line)</li>
          <li>Employees with accuracy {'<'}95% should receive additional training</li>
          <li>Locations with consistently low accuracy may need process improvements</li>
          <li>Schedule more frequent counts in locations with high variance</li>
        </ul>
      </div>
    </div>
  );
};

export default CycleCountAccuracy;
