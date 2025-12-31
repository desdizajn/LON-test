import axios from 'axios';

const API_BASE_URL = process.env.REACT_APP_API_URL || 'http://localhost:5000/api';

export interface LoginRequest {
  username: string;
  password: string;
}

export interface LoginResponse {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
  user: {
    id: string;
    username: string;
    email: string;
    isActive: boolean;
    lastLoginAt?: string;
    employee?: any;
    roles: Array<{
      id: string;
      name: string;
      description?: string;
    }>;
    createdAt: string;
  };
}

export interface User {
  id: string;
  username: string;
  email: string;
  fullName: string;
  isActive: boolean;
  roles: string[];
  permissions: string[];
  lastLogin?: string;
  createdAt?: string;
}

export interface CreateUserRequest {
  username: string;
  email: string;
  fullName: string;
  password: string;
  roleIds: string[];
}

export interface UpdateUserRequest {
  email: string;
  fullName: string;
  isActive: boolean;
  roleIds: string[];
}

export interface Role {
  id: string;
  name: string;
  description?: string;
  permissions: Permission[];
}

export interface Permission {
  id: string;
  name: string;
  description?: string;
  resource: string;
  action: string;
}

class AuthService {
  private token: string | null = null;

  constructor() {
    this.token = localStorage.getItem('auth_token');
    if (this.token) {
      this.setAuthHeader(this.token);
    }
  }

  private setAuthHeader(token: string) {
    axios.defaults.headers.common['Authorization'] = `Bearer ${token}`;
  }

  private removeAuthHeader() {
    delete axios.defaults.headers.common['Authorization'];
  }

  async login(credentials: LoginRequest): Promise<LoginResponse> {
    try {
      const response = await axios.post<LoginResponse>(
        `${API_BASE_URL}/auth/login`,
        credentials
      );
      
      this.token = response.data.accessToken;
      localStorage.setItem('auth_token', this.token);
      
      // Transform user data for local storage
      const userData = {
        id: response.data.user.id,
        username: response.data.user.username,
        email: response.data.user.email,
        fullName: response.data.user.username, // Backend doesn't return fullName
        isActive: response.data.user.isActive,
        roles: response.data.user.roles.map(r => r.name),
        permissions: [], // Will be populated from roles
        lastLogin: response.data.user.lastLoginAt
      };
      
      localStorage.setItem('user', JSON.stringify(userData));
      this.setAuthHeader(this.token);
      
      return response.data;
    } catch (error) {
      console.error('Login failed:', error);
      throw error;
    }
  }

  logout() {
    this.token = null;
    localStorage.removeItem('auth_token');
    localStorage.removeItem('user');
    this.removeAuthHeader();
  }

  getCurrentUser(): User | null {
    const userStr = localStorage.getItem('user');
    return userStr ? JSON.parse(userStr) : null;
  }

  isAuthenticated(): boolean {
    return !!this.token;
  }

  getToken(): string | null {
    return this.token;
  }

  hasPermission(permission: string): boolean {
    const user = this.getCurrentUser();
    return user?.permissions.includes(permission) || false;
  }

  hasRole(role: string): boolean {
    const user = this.getCurrentUser();
    return user?.roles.includes(role) || false;
  }

  // Users API
  async getUsers(): Promise<User[]> {
    const response = await axios.get<User[]>(`${API_BASE_URL}/users`);
    return response.data;
  }

  async getUserById(id: string): Promise<User> {
    const response = await axios.get<User>(`${API_BASE_URL}/users/${id}`);
    return response.data;
  }

  async createUser(user: CreateUserRequest): Promise<User> {
    const response = await axios.post<User>(`${API_BASE_URL}/users`, user);
    return response.data;
  }

  async updateUser(id: string, user: UpdateUserRequest): Promise<User> {
    const response = await axios.put<User>(`${API_BASE_URL}/users/${id}`, user);
    return response.data;
  }

  async deleteUser(id: string): Promise<void> {
    await axios.delete(`${API_BASE_URL}/users/${id}`);
  }

  async changePassword(userId: string, currentPassword: string, newPassword: string): Promise<void> {
    await axios.post(`${API_BASE_URL}/users/${userId}/change-password`, {
      currentPassword,
      newPassword
    });
  }

  // Roles API
  async getRoles(): Promise<Role[]> {
    const response = await axios.get<Role[]>(`${API_BASE_URL}/roles`);
    return response.data;
  }

  async getRoleById(id: string): Promise<Role> {
    const response = await axios.get<Role>(`${API_BASE_URL}/roles/${id}`);
    return response.data;
  }

  async createRole(role: { name: string; description?: string; permissionIds: string[] }): Promise<Role> {
    const response = await axios.post<Role>(`${API_BASE_URL}/roles`, role);
    return response.data;
  }

  async updateRole(id: string, role: { name: string; description?: string; permissionIds: string[] }): Promise<Role> {
    const response = await axios.put<Role>(`${API_BASE_URL}/roles/${id}`, role);
    return response.data;
  }

  async deleteRole(id: string): Promise<void> {
    await axios.delete(`${API_BASE_URL}/roles/${id}`);
  }

  // Permissions API
  async getPermissions(): Promise<Permission[]> {
    const response = await axios.get<Permission[]>(`${API_BASE_URL}/permissions`);
    return response.data;
  }
}

export const authService = new AuthService();
export default authService;
