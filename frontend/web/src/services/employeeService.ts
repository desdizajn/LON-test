import axios from 'axios';

const API_BASE_URL = process.env.REACT_APP_API_URL || 'http://localhost:5000/api';

export interface Employee {
  id: string;
  userId: string;
  firstName: string;
  lastName: string;
  email: string;
  phone?: string;
  position: string;
  department: string;
  hireDate: string;
  isActive: boolean;
  user?: {
    username: string;
    fullName: string;
  };
}

export interface CreateEmployeeRequest {
  userId: string;
  firstName: string;
  lastName: string;
  email: string;
  phone?: string;
  position: string;
  department: string;
  hireDate: string;
}

export interface UpdateEmployeeRequest {
  firstName: string;
  lastName: string;
  email: string;
  phone?: string;
  position: string;
  department: string;
  isActive: boolean;
}

export interface Shift {
  id: string;
  name: string;
  startTime: string;
  endTime: string;
  description?: string;
  isActive: boolean;
}

export interface CreateShiftRequest {
  name: string;
  startTime: string;
  endTime: string;
  description?: string;
}

export interface UpdateShiftRequest {
  name: string;
  startTime: string;
  endTime: string;
  description?: string;
  isActive: boolean;
}

export interface EmployeeShift {
  id: string;
  employeeId: string;
  shiftId: string;
  effectiveDate: string;
  endDate?: string;
  employee?: Employee;
  shift?: Shift;
}

class EmployeeService {
  // Employees
  async getEmployees(): Promise<Employee[]> {
    const response = await axios.get<Employee[]>(`${API_BASE_URL}/employees`);
    return response.data;
  }

  async getEmployeeById(id: string): Promise<Employee> {
    const response = await axios.get<Employee>(`${API_BASE_URL}/employees/${id}`);
    return response.data;
  }

  async createEmployee(employee: CreateEmployeeRequest): Promise<Employee> {
    const response = await axios.post<Employee>(`${API_BASE_URL}/employees`, employee);
    return response.data;
  }

  async updateEmployee(id: string, employee: UpdateEmployeeRequest): Promise<Employee> {
    const response = await axios.put<Employee>(`${API_BASE_URL}/employees/${id}`, employee);
    return response.data;
  }

  async deleteEmployee(id: string): Promise<void> {
    await axios.delete(`${API_BASE_URL}/employees/${id}`);
  }

  // Shifts
  async getShifts(): Promise<Shift[]> {
    const response = await axios.get<Shift[]>(`${API_BASE_URL}/shifts`);
    return response.data;
  }

  async getShiftById(id: string): Promise<Shift> {
    const response = await axios.get<Shift>(`${API_BASE_URL}/shifts/${id}`);
    return response.data;
  }

  async createShift(shift: CreateShiftRequest): Promise<Shift> {
    const response = await axios.post<Shift>(`${API_BASE_URL}/shifts`, shift);
    return response.data;
  }

  async updateShift(id: string, shift: UpdateShiftRequest): Promise<Shift> {
    const response = await axios.put<Shift>(`${API_BASE_URL}/shifts/${id}`, shift);
    return response.data;
  }

  async deleteShift(id: string): Promise<void> {
    await axios.delete(`${API_BASE_URL}/shifts/${id}`);
  }

  // Employee Shifts
  async getEmployeeShifts(employeeId: string): Promise<EmployeeShift[]> {
    const response = await axios.get<EmployeeShift[]>(`${API_BASE_URL}/employees/${employeeId}/shifts`);
    return response.data;
  }

  async assignShift(employeeId: string, shiftId: string, effectiveDate: string): Promise<EmployeeShift> {
    const response = await axios.post<EmployeeShift>(`${API_BASE_URL}/employees/${employeeId}/shifts`, {
      shiftId,
      effectiveDate
    });
    return response.data;
  }

  async endShiftAssignment(employeeShiftId: string, endDate: string): Promise<void> {
    await axios.put(`${API_BASE_URL}/employee-shifts/${employeeShiftId}/end`, { endDate });
  }
}

export const employeeService = new EmployeeService();
export default employeeService;
