import { DocumentItem } from './documentItem';

export interface Document {
    id: string;
    title: string;
    children: DocumentItem[]
  }